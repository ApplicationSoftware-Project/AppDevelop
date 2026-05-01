using App.Features.AI.Data;
using App.Features.AI.Services;
using App.Features.Receipt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace App.Features.Receipt;

public class ReceiptService(
    OcrService ocrService,
    AiSuggestionService aiSuggestionService,
    IWebHostEnvironment env,
    ILogger<ReceiptService> logger)
{
    private const string StorageSubPath = "storage/receipts";

    public async Task<UploadReceiptResult> ProcessAsync(
        Guid userId,
        IFormFile file,
        Kernel kernel,
        AppDbContext db,
        CancellationToken ct = default)
    {
        var receiptId = Guid.NewGuid();
        var (relativePath, absolutePath) = BuildPaths(userId, receiptId, file.FileName);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await using (var fs = File.Create(absolutePath))
        {
            await file.CopyToAsync(fs, ct);
        }

        var ocr = await ocrService.ParseAsync(absolutePath, ct);

        var receipt = new Models.Receipt
        {
            Id = receiptId,
            UserId = userId,
            StoreName = string.IsNullOrWhiteSpace(ocr.StoreName) ? "알 수 없는 상점" : ocr.StoreName,
            Amount = ocr.Amount,
            PurchasedAt = ocr.PurchasedAt,
            ImagePath = relativePath,
            ContentType = file.ContentType,
            RawOcrText = string.IsNullOrWhiteSpace(ocr.RawText) ? null : ocr.RawText,
            Status = ReceiptStatus.OcrProcessed
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        string? suggestedCategory = null;
        double? confidence = null;
        Guid? aiLogId = null;

        if (!string.IsNullOrWhiteSpace(ocr.RawText))
        {
            try
            {
                var aiRequest = new AI.SuggestCategoryRequest(receipt.Id, ocr.RawText);
                var aiResult = await aiSuggestionService.SuggestCategoryAsync(aiRequest, kernel, db);
                suggestedCategory = aiResult.Category;
                confidence = aiResult.Confidence;
                aiLogId = aiResult.LogId;

                receipt.AiSuggestedCategory = suggestedCategory;
                receipt.AiLogId = aiLogId;
                receipt.Status = ReceiptStatus.AiCategorized;
                receipt.ProcessedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI 카테고리 추천 실패. ReceiptId={ReceiptId} (OCR 결과는 보존됨)", receipt.Id);
            }
        }

        return new UploadReceiptResult(receipt.Id, relativePath, ocr, suggestedCategory, confidence, aiLogId, receipt.Status);
    }

    public async Task<ReceiptListResult> GetListAsync(Guid userId, int page, int pageSize, AppDbContext db)
    {
        var query = db.Receipts.AsNoTracking().Where(r => r.UserId == userId);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.PurchasedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReceiptSummary(
                r.Id, r.StoreName, r.Amount, r.PurchasedAt,
                r.Category, r.AiSuggestedCategory, r.Status, r.CreatedAt))
            .ToListAsync();

        return new ReceiptListResult(total, items);
    }

    public async Task<ReceiptDetail?> GetDetailAsync(Guid receiptId, Guid userId, AppDbContext db)
    {
        return await db.Receipts
            .AsNoTracking()
            .Where(r => r.Id == receiptId && r.UserId == userId)
            .Select(r => new ReceiptDetail(
                r.Id, r.StoreName, r.Amount, r.PurchasedAt,
                r.Category, r.AiSuggestedCategory, r.Status,
                r.RawOcrText, r.ContentType, r.CreatedAt, r.ProcessedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<(string AbsolutePath, string ContentType)?> GetImageAsync(Guid receiptId, Guid userId, AppDbContext db)
    {
        var row = await db.Receipts
            .AsNoTracking()
            .Where(r => r.Id == receiptId && r.UserId == userId)
            .Select(r => new { r.ImagePath, r.ContentType })
            .FirstOrDefaultAsync();

        if (row is null || string.IsNullOrEmpty(row.ImagePath)) return null;

        var absolute = ResolveAbsolute(row.ImagePath);
        if (!File.Exists(absolute))
        {
            logger.LogWarning("영수증 이미지 파일이 디스크에 없습니다. ReceiptId={ReceiptId}, Path={Path}", receiptId, absolute);
            return null;
        }

        return (absolute, row.ContentType ?? "application/octet-stream");
    }

    public async Task<bool> DeleteAsync(Guid receiptId, Guid userId, AppDbContext db, CancellationToken ct = default)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.UserId == userId, ct);
        if (receipt is null) return false;

        if (!string.IsNullOrEmpty(receipt.ImagePath))
        {
            var absolute = ResolveAbsolute(receipt.ImagePath);
            try
            {
                if (File.Exists(absolute)) File.Delete(absolute);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "영수증 이미지 파일 삭제 실패. ReceiptId={ReceiptId}, Path={Path}", receiptId, absolute);
            }
        }

        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConfirmReceiptCategoryResult?> ConfirmCategoryAsync(
        Guid receiptId, Guid userId, string finalCategory, AppDbContext db)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId && r.UserId == userId);
        if (receipt is null) return null;

        receipt.Category = finalCategory;
        receipt.Status = ReceiptStatus.Confirmed;

        var aiWasCorrect = receipt.AiSuggestedCategory == finalCategory;
        await db.SaveChangesAsync();

        return new ConfirmReceiptCategoryResult(receiptId, finalCategory, aiWasCorrect);
    }

    private string ResolveAbsolute(string relativePath) =>
        Path.Combine(env.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private (string RelativePath, string AbsolutePath) BuildPaths(Guid userId, Guid receiptId, string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var fileName = $"{receiptId:N}{ext}";
        var relative = $"{StorageSubPath}/{userId:N}/{fileName}";
        var absolute = Path.Combine(env.ContentRootPath, StorageSubPath, userId.ToString("N"), fileName);
        return (relative, absolute);
    }
}

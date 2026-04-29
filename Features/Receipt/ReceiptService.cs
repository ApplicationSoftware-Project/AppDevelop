using App.Features.AI.Data;
using App.Features.AI.Models;
using App.Features.AI.Services;
using App.Features.Receipt.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace App.Features.Receipt;

public class ReceiptService(OcrService ocrService, AiSuggestionService aiSuggestionService)
{
    public async Task<UploadReceiptResult> ProcessAsync(
        Guid userId,
        UploadReceiptRequest request,
        Kernel kernel,
        AppDbContext db)
    {
        var ocr = ocrService.Parse(request.RawText);

        var receipt = new Models.Receipt
        {
            UserId = userId,
            StoreName = request.StoreName ?? ocr.StoreName,
            Amount = request.Amount ?? ocr.Amount,
            PurchasedAt = request.PurchasedAt ?? ocr.PurchasedAt,
            RawOcrText = request.RawText,
            Status = ReceiptStatus.OcrProcessed
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync();

        string? suggestedCategory = null;
        double? confidence = null;
        Guid? aiLogId = null;

        try
        {
            var aiRequest = new AI.SuggestCategoryRequest(receipt.Id, request.RawText);
            var aiResult = await aiSuggestionService.SuggestCategoryAsync(aiRequest, kernel, db);
            suggestedCategory = aiResult.Category;
            confidence = aiResult.Confidence;
            aiLogId = aiResult.LogId;

            receipt.AiSuggestedCategory = suggestedCategory;
            receipt.AiLogId = aiLogId;
            receipt.Status = ReceiptStatus.AiCategorized;
            receipt.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        catch
        {
            // AI 실패해도 OCR 결과는 저장
        }

        return new UploadReceiptResult(receipt.Id, ocr, suggestedCategory, confidence, aiLogId, receipt.Status);
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
}
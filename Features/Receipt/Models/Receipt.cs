namespace App.Features.Receipt.Models;

public class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string StoreName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateTimeOffset? PurchasedAt { get; set; }

    public string? ImagePath { get; set; }
    public string? ContentType { get; set; }

    public string? RawOcrText { get; set; }
    public string? Category { get; set; }
    public string? AiSuggestedCategory { get; set; }
    public Guid? AiLogId { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }

    // [추가] 영수증 기초 정보 수정 시 기록되는 타임스탬프
    public DateTimeOffset? UpdatedAt { get; set; }
}

public enum ReceiptStatus
{
    Pending,
    OcrProcessed,
    AiCategorized,
    Confirmed
}
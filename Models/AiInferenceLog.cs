using System;

namespace App.Models
{
    public class AiInferenceLog
    {
        public Guid Id { get; set; }
        public Guid ReceiptId { get; set; }
        public string SuggestedCategory { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string? FinalCategory { get; set; }
        public bool? IsCorrect { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public AiInferenceLog()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}
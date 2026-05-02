using App.Features.AI.Models;
using App.Features.Auth.Models;
using Microsoft.EntityFrameworkCore;
using ReceiptModel = App.Features.Receipt.Models.Receipt;

namespace App.Features.AI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AiInferenceLog> AiInferenceLogs { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ReceiptModel> Receipts { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AiInferenceLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SuggestedCategory).IsRequired().HasMaxLength(200);
                entity.Property(e => e.FinalCategory).HasMaxLength(200);
                entity.Property(e => e.Confidence).HasPrecision(5, 4);
                entity.Property(e => e.CreatedAt).IsRequired();
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.PasswordHash).IsRequired();
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(50).HasDefaultValue("User");
                entity.Property(e => e.CreatedAt).IsRequired();

                // 추가 필드
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.ProfileImageUrl).HasMaxLength(512);
                entity.Property(e => e.RefreshToken).HasMaxLength(512);

                // [버그 수정 6] 알림 기본값 true로 수정
                entity.Property(e => e.EmailNotification).HasDefaultValue(true);
                entity.Property(e => e.PushNotification).HasDefaultValue(true);
            });

            modelBuilder.Entity<ReceiptModel>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StoreName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.ImagePath).HasMaxLength(500);
                entity.Property(e => e.ContentType).HasMaxLength(100);
                entity.Property(e => e.Category).HasMaxLength(200);
                entity.Property(e => e.AiSuggestedCategory).HasMaxLength(200);
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.HasIndex(e => new { e.UserId, e.PurchasedAt });
            });
        }
    }
}
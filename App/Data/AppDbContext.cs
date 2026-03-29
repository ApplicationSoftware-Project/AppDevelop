using App.Models;
using Microsoft.EntityFrameworkCore;

namespace App.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AiInferenceLog> AiInferenceLogs { get; set; } = null!;

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
        }
    }
}
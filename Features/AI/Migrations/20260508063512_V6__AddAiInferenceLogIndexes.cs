using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Features.AI.Migrations
{
    /// <inheritdoc />
    public partial class V6__AddAiInferenceLogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceLogs_IsCorrect_CreatedAt",
                table: "AiInferenceLogs",
                columns: new[] { "IsCorrect", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceLogs_ReceiptId",
                table: "AiInferenceLogs",
                column: "ReceiptId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AiInferenceLogs_IsCorrect_CreatedAt",
                table: "AiInferenceLogs");

            migrationBuilder.DropIndex(
                name: "IX_AiInferenceLogs_ReceiptId",
                table: "AiInferenceLogs");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Features.AI.Migrations
{
    /// <inheritdoc />
    public partial class V3__AddReceiptImageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Receipts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Receipts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Receipts");
        }
    }
}

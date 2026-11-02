using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseShow.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreatedByImportBatchId",
                table: "Shows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByImportBatchId",
                table: "ShowClasses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shows_CreatedByImportBatchId",
                table: "Shows",
                column: "CreatedByImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowClasses_CreatedByImportBatchId",
                table: "ShowClasses",
                column: "CreatedByImportBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shows_CreatedByImportBatchId",
                table: "Shows");

            migrationBuilder.DropIndex(
                name: "IX_ShowClasses_CreatedByImportBatchId",
                table: "ShowClasses");

            migrationBuilder.DropColumn(
                name: "CreatedByImportBatchId",
                table: "Shows");

            migrationBuilder.DropColumn(
                name: "CreatedByImportBatchId",
                table: "ShowClasses");
        }
    }
}

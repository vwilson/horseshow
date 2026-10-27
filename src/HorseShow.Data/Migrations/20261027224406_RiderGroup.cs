using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HorseShow.Data.Migrations
{
    /// <inheritdoc />
    public partial class RiderGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Riders",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Riders_Group",
                table: "Riders",
                column: "Group");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Riders_Group",
                table: "Riders");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Riders");
        }
    }
}

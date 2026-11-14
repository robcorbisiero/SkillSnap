using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillSnap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "LearningSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "LearningSessions",
                keyColumn: "Id",
                keyValue: 1,
                column: "Version",
                value: 0);

            migrationBuilder.UpdateData(
                table: "LearningSessions",
                keyColumn: "Id",
                keyValue: 2,
                column: "Version",
                value: 0);

            migrationBuilder.UpdateData(
                table: "LearningSessions",
                keyColumn: "Id",
                keyValue: 3,
                column: "Version",
                value: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "LearningSessions");
        }
    }
}

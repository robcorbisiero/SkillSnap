using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SkillSnap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Instructor = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningSessions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LearningSessions",
                columns: new[] { "Id", "Capacity", "Description", "DurationMinutes", "Instructor", "StartsAtUtc", "Title", "Topic" },
                values: new object[,]
                {
                    { 1, 20, "Explore Razor components, parameters, and events by building a small interactive view.", 60, "Alex Morgan", new DateTime(2027, 1, 12, 18, 0, 0, 0, DateTimeKind.Utc), "Build your first Blazor component", "Blazor" },
                    { 2, 16, "Follow a request through an ASP.NET Core endpoint and return a clear JSON response.", 90, "Sam Rivera", new DateTime(2027, 1, 14, 19, 0, 0, 0, DateTimeKind.Utc), "APIs that make sense", "ASP.NET Core" },
                    { 3, 24, "Use a DbContext, a migration, and an asynchronous query to read real persisted data.", 75, "Taylor Chen", new DateTime(2027, 1, 19, 18, 30, 0, 0, DateTimeKind.Utc), "From C# objects to SQLite", "Entity Framework Core" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningSessions_StartsAtUtc",
                table: "LearningSessions",
                column: "StartsAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningSessions");
        }
    }
}

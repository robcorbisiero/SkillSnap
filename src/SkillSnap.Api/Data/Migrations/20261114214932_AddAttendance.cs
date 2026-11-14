using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillSnap.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendanceStatus",
                table: "Registrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceUpdatedAtUtc",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceUpdatedByUserId",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttendanceVersion",
                table: "Registrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Registration_Attendance",
                table: "Registrations",
                sql: "\"AttendanceStatus\" IN (0,1,2) AND \"AttendanceVersion\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Registration_Attendance",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "AttendanceStatus",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "AttendanceUpdatedAtUtc",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "AttendanceUpdatedByUserId",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "AttendanceVersion",
                table: "Registrations");
        }
    }
}

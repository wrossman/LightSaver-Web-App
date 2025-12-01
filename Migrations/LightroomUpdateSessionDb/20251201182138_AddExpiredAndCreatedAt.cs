using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations.LightroomUpdateSessionDb
{
    /// <inheritdoc />
    public partial class AddExpiredAndCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "UpdateSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "Expired",
                table: "UpdateSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UpdateSessions");

            migrationBuilder.DropColumn(
                name: "Expired",
                table: "UpdateSessions");
        }
    }
}

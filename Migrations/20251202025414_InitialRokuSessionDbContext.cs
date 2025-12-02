using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations
{
    /// <inheritdoc />
    public partial class InitialRokuSessionDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RokuSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RokuId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceAddress = table.Column<string>(type: "text", nullable: false),
                    SessionCode = table.Column<string>(type: "text", nullable: false),
                    ReadyForTransfer = table.Column<bool>(type: "boolean", nullable: false),
                    Expired = table.Column<bool>(type: "boolean", nullable: false),
                    MaxScreenSize = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RokuSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RokuSessions_SessionCode",
                table: "RokuSessions",
                column: "SessionCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RokuSessions");
        }
    }
}

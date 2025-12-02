using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations.LightroomUpdateSessionDb
{
    /// <inheritdoc />
    public partial class InitialLightroomUpdateSessionDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.CreateTable(
                name: "UpdateSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    RokuId = table.Column<string>(type: "text", nullable: false),
                    ReadyForTransfer = table.Column<bool>(type: "boolean", nullable: false),
                    Links = table.Column<Dictionary<string, string>>(type: "hstore", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Expired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateSessions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdateSessions");
        }
    }
}

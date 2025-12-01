using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations.GlobalImageStoreDb
{
    /// <inheritdoc />
    public partial class InitialGlobalImageStoreDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    SessionCode = table.Column<string>(type: "text", nullable: false),
                    ImageStream = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileType = table.Column<string>(type: "text", nullable: false),
                    RokuId = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    OriginUrl = table.Column<string>(type: "text", nullable: false),
                    LightroomAlbum = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Resources");
        }
    }
}

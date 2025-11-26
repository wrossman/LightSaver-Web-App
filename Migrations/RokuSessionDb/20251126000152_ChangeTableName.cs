using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations.RokuSessionDb
{
    /// <inheritdoc />
    public partial class ChangeTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Sessions",
                table: "Sessions");

            migrationBuilder.RenameTable(
                name: "Sessions",
                newName: "RokuSessions");

            migrationBuilder.RenameIndex(
                name: "IX_Sessions_SessionCode",
                table: "RokuSessions",
                newName: "IX_RokuSessions_SessionCode");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RokuSessions",
                table: "RokuSessions",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RokuSessions",
                table: "RokuSessions");

            migrationBuilder.RenameTable(
                name: "RokuSessions",
                newName: "Sessions");

            migrationBuilder.RenameIndex(
                name: "IX_RokuSessions_SessionCode",
                table: "Sessions",
                newName: "IX_Sessions_SessionCode");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sessions",
                table: "Sessions",
                column: "Id");
        }
    }
}

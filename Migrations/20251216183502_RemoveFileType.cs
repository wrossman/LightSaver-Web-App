using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFileType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileType",
                table: "Resources");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "Resources",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

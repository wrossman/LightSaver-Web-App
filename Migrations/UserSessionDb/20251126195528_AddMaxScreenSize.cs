using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LightSaver.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxScreenSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxScreenSize",
                table: "UserSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxScreenSize",
                table: "UserSessions");
        }
    }
}

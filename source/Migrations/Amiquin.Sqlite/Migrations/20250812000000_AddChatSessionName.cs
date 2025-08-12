using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ChatSessions",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "Default Session");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "ChatSessions");
        }
    }
}
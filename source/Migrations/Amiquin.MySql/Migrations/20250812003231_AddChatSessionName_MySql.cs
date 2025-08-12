using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionName_MySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ChatSessions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Default Session")
                .Annotation("MySql:CharSet", "utf8mb4");
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

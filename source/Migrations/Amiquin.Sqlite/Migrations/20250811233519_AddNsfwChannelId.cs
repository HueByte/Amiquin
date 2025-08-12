using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddNsfwChannelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "NsfwChannelId",
                table: "ServerMetas",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NsfwChannelId",
                table: "ServerMetas");
        }
    }
}
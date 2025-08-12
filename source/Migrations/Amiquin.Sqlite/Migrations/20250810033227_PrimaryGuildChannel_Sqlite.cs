using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class PrimaryGuildChannel_Sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "PrimaryChannelId",
                table: "ServerMetas",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrimaryChannelId",
                table: "ServerMetas");
        }
    }
}
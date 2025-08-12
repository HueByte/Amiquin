using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.MySql.Migrations
{
    /// <inheritdoc />
    public partial class PrimaryGuildChannel_MySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "PrimaryChannelId",
                table: "ServerMetas",
                type: "bigint unsigned",
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
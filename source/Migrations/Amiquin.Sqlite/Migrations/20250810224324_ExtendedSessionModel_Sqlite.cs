using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ExtendedSessionModel_Sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredProvider",
                table: "ServerMetas",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreferredProvider",
                table: "ServerMetas");
        }
    }
}
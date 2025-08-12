using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ExtendedSessionModel_MySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredProvider",
                table: "ServerMetas",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
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
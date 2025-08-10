using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Sqlite.Migrations.Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ServerMetaModelCorrection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AIModel",
                table: "ServerMetas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AIModel",
                table: "ServerMetas",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class UserStats_Sqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    server_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    fun_stats = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_stats_user_id_server_id",
                table: "user_stats",
                columns: new[] { "user_id", "server_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_stats");
        }
    }
}
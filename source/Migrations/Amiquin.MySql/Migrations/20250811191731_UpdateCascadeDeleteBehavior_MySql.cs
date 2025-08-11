using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.MySql.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCascadeDeleteBehavior_MySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommandLogs_ServerMetas_ServerId",
                table: "CommandLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_NachoPacks_ServerMetas_ServerId",
                table: "NachoPacks");

            migrationBuilder.AlterColumn<ulong>(
                name: "ServerId",
                table: "NachoPacks",
                type: "bigint unsigned",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AlterColumn<ulong>(
                name: "ServerId",
                table: "CommandLogs",
                type: "bigint unsigned",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned");

            migrationBuilder.AddForeignKey(
                name: "FK_CommandLogs_ServerMetas_ServerId",
                table: "CommandLogs",
                column: "ServerId",
                principalTable: "ServerMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NachoPacks_ServerMetas_ServerId",
                table: "NachoPacks",
                column: "ServerId",
                principalTable: "ServerMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CommandLogs_ServerMetas_ServerId",
                table: "CommandLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_NachoPacks_ServerMetas_ServerId",
                table: "NachoPacks");

            migrationBuilder.AlterColumn<ulong>(
                name: "ServerId",
                table: "NachoPacks",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.AlterColumn<ulong>(
                name: "ServerId",
                table: "CommandLogs",
                type: "bigint unsigned",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "bigint unsigned",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CommandLogs_ServerMetas_ServerId",
                table: "CommandLogs",
                column: "ServerId",
                principalTable: "ServerMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NachoPacks_ServerMetas_ServerId",
                table: "NachoPacks",
                column: "ServerId",
                principalTable: "ServerMetas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

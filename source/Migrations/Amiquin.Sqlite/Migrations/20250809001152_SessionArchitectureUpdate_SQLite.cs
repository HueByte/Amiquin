using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SessionArchitectureUpdate_SQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    OwningEntityId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    ServerId = table.Column<ulong>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSessions_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SessionMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChatSessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscordMessageId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EstimatedTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    IncludeInContext = table.Column<bool>(type: "INTEGER", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionMessages_ChatSessions_ChatSessionId",
                        column: x => x.ChatSessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_Activity",
                table: "ChatSessions",
                columns: new[] { "IsActive", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_Created",
                table: "ChatSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_Scope_Owner",
                table: "ChatSessions",
                columns: new[] { "Scope", "OwningEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ServerId",
                table: "ChatSessions",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_SessionId",
                table: "ChatSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionMessages_ChatSessionId",
                table: "SessionMessages",
                column: "ChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionMessages_CreatedAt",
                table: "SessionMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SessionMessages_DiscordMessageId",
                table: "SessionMessages",
                column: "DiscordMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionMessages");

            migrationBuilder.DropTable(
                name: "ChatSessions");
        }
    }
}

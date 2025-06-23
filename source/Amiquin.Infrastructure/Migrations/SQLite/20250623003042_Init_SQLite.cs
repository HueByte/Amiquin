using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Infrastructure.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class Init_SQLite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotStatistics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TotalServersCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalChannelsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalUsersCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCommandsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalErrorsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageCommandExecutionTimeInMs = table.Column<double>(type: "REAL", nullable: false),
                    Latency = table.Column<int>(type: "INTEGER", nullable: false),
                    ShardCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpTimeInSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CacheItems = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailableMemoryMB = table.Column<float>(type: "REAL", nullable: false),
                    UsedMemoryMB = table.Column<float>(type: "REAL", nullable: false),
                    UsedMemoryPercentage = table.Column<float>(type: "REAL", nullable: false),
                    CpuUsage = table.Column<double>(type: "REAL", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", nullable: true),
                    BotName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerMetas",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServerName = table.Column<string>(type: "TEXT", nullable: false),
                    Persona = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMetas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Command = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CommandDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Duration = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ServerId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandLogs_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IsUser = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ServerId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NachoPacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NachoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    NachoReceivedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ServerId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NachoPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NachoPacks_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Toggles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    ServerId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Toggles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Toggles_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_ServerId",
                table: "CommandLogs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ServerId",
                table: "Messages",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_NachoPacks_ServerId",
                table: "NachoPacks",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Toggles_ServerId",
                table: "Toggles",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotStatistics");

            migrationBuilder.DropTable(
                name: "CommandLogs");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "NachoPacks");

            migrationBuilder.DropTable(
                name: "Toggles");

            migrationBuilder.DropTable(
                name: "ServerMetas");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.MySql.Migrations
{
    /// <inheritdoc />
    public partial class Initial_MySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BotStatistics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalServersCount = table.Column<int>(type: "int", nullable: false),
                    TotalChannelsCount = table.Column<int>(type: "int", nullable: false),
                    TotalUsersCount = table.Column<int>(type: "int", nullable: false),
                    TotalCommandsCount = table.Column<int>(type: "int", nullable: false),
                    TotalErrorsCount = table.Column<int>(type: "int", nullable: false),
                    AverageCommandExecutionTimeInMs = table.Column<double>(type: "double", nullable: false),
                    Latency = table.Column<int>(type: "int", nullable: false),
                    ShardCount = table.Column<int>(type: "int", nullable: false),
                    UpTimeInSeconds = table.Column<int>(type: "int", nullable: false),
                    CacheItems = table.Column<int>(type: "int", nullable: false),
                    AvailableMemoryMB = table.Column<float>(type: "float", nullable: false),
                    UsedMemoryMB = table.Column<float>(type: "float", nullable: false),
                    UsedMemoryPercentage = table.Column<float>(type: "float", nullable: false),
                    CpuUsage = table.Column<double>(type: "double", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BotName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotStatistics", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GlobalToggles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AllowServerOverride = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Category = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalToggles", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PaginationSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CurrentPage = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    ChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    MessageId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    EmbedData = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalPages = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaginationSessions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ServerMetas",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ServerName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Persona = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PrimaryChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: true),
                    PreferredProvider = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreferredModel = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NsfwChannelId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerMetas", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    server_id = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    fun_stats = table.Column<string>(type: "TEXT", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_stats", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OwningEntityId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedTokens = table.Column<int>(type: "int", nullable: false),
                    Model = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Context = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContextTokens = table.Column<int>(type: "int", nullable: false),
                    Metadata = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ServerId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSessions_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CommandLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Command = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Username = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    CommandDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Duration = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ServerId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandLogs_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    AuthorId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsUser = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ServerId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NachoPacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NachoCount = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    NachoReceivedDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ServerId = table.Column<ulong>(type: "bigint unsigned", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NachoPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NachoPacks_ServerMetas_ServerId",
                        column: x => x.ServerId,
                        principalTable: "ServerMetas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Toggles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ServerId = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SessionMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChatSessionId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DiscordMessageId = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EstimatedTokens = table.Column<int>(type: "int", nullable: false),
                    IncludeInContext = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Metadata = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
                name: "IX_CommandLogs_ServerId",
                table: "CommandLogs",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalToggles_Category",
                table: "GlobalToggles",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalToggles_Name",
                table: "GlobalToggles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ServerId",
                table: "Messages",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_NachoPacks_ServerId",
                table: "NachoPacks",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaginationSessions_ExpiresAt",
                table: "PaginationSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaginationSessions_MessageId",
                table: "PaginationSessions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PaginationSessions_UserActive",
                table: "PaginationSessions",
                columns: new[] { "UserId", "IsActive" });

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

            migrationBuilder.CreateIndex(
                name: "IX_Toggles_ServerId",
                table: "Toggles",
                column: "ServerId");

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
                name: "BotStatistics");

            migrationBuilder.DropTable(
                name: "CommandLogs");

            migrationBuilder.DropTable(
                name: "GlobalToggles");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "NachoPacks");

            migrationBuilder.DropTable(
                name: "PaginationSessions");

            migrationBuilder.DropTable(
                name: "SessionMessages");

            migrationBuilder.DropTable(
                name: "Toggles");

            migrationBuilder.DropTable(
                name: "user_stats");

            migrationBuilder.DropTable(
                name: "ChatSessions");

            migrationBuilder.DropTable(
                name: "ServerMetas");
        }
    }
}

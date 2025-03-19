using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Amiquin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Rename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "Messages",
                newName: "InstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InstanceId",
                table: "Messages",
                newName: "ChannelId");
        }
    }
}

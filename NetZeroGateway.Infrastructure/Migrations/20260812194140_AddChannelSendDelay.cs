using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelSendDelay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "delay_seconds",
                table: "channel_configs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delay_seconds",
                table: "channel_configs");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    variable_labels = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_channel_templates_channel_configs_channel_config_id",
                        column: x => x.channel_config_id,
                        principalTable: "channel_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channel_templates_channel_config_id_name",
                table: "channel_templates",
                columns: new[] { "channel_config_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_templates");
        }
    }
}

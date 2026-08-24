using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <summary>
    /// Lets a fallback rule name specific channels, and makes crossing channel types
    /// opt-in. Both new id columns are nullable, where null keeps the previous meaning:
    /// "any channel of the trigger type" and "the fallback type's default channel", so
    /// existing rules keep behaving exactly as before.
    /// </summary>
    public partial class AddFallbackChannelTargeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allow_cross_channel",
                table: "fallback_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "fallback_channel_config_id",
                table: "fallback_policies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "trigger_channel_config_id",
                table: "fallback_policies",
                type: "uuid",
                nullable: true);

            // Every policy that existed before this change crosses channel types (the
            // seeded rule is WhatsApp -> SMS). allow_cross_channel defaults to false and
            // the dispatcher skips cross-type policies without it, so leaving these rows
            // at the default would silently switch off live fallback behaviour.
            migrationBuilder.Sql("""
                UPDATE fallback_policies
                SET allow_cross_channel = true
                WHERE trigger_channel <> fallback_channel;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_fallback_policies_fallback_channel_config_id",
                table: "fallback_policies",
                column: "fallback_channel_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_fallback_policies_trigger_channel_config_id",
                table: "fallback_policies",
                column: "trigger_channel_config_id");

            migrationBuilder.AddForeignKey(
                name: "fk_fallback_policies_channel_configs_fallback_channel_config_id",
                table: "fallback_policies",
                column: "fallback_channel_config_id",
                principalTable: "channel_configs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fallback_policies_channel_configs_trigger_channel_config_id",
                table: "fallback_policies",
                column: "trigger_channel_config_id",
                principalTable: "channel_configs",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_fallback_policies_channel_configs_fallback_channel_config_id",
                table: "fallback_policies");

            migrationBuilder.DropForeignKey(
                name: "fk_fallback_policies_channel_configs_trigger_channel_config_id",
                table: "fallback_policies");

            migrationBuilder.DropIndex(
                name: "ix_fallback_policies_fallback_channel_config_id",
                table: "fallback_policies");

            migrationBuilder.DropIndex(
                name: "ix_fallback_policies_trigger_channel_config_id",
                table: "fallback_policies");

            migrationBuilder.DropColumn(
                name: "allow_cross_channel",
                table: "fallback_policies");

            migrationBuilder.DropColumn(
                name: "fallback_channel_config_id",
                table: "fallback_policies");

            migrationBuilder.DropColumn(
                name: "trigger_channel_config_id",
                table: "fallback_policies");
        }
    }
}

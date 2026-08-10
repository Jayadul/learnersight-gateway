using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <summary>
    /// Collapses the WP_Text and WP_Media channel types into a single WP.
    ///
    /// Pure data migration — channel_type is persisted as a string (HasConversion&lt;string&gt;),
    /// so no column changes are needed, only value rewrites. Order matters:
    ///
    ///   1. Repoint dispatch jobs off the WP_Media config (FK would block its delete).
    ///   2. Delete the WP_Media config rows — required, because both WhatsApp configs are
    ///      active today and the unique partial index
    ///      (channel_type, is_active) WHERE is_active allows only one active row per type.
    ///   3. Rewrite the type strings everywhere they are stored.
    ///   4. Drop fallback policies that would now point a channel at itself.
    ///   5. Rewrite the channel names cached in message_requests.parsed_channels (jsonb).
    ///
    /// NOT REVERSIBLE: Down() restores the WP_Text/WP_Media names but cannot resurrect the
    /// deleted WP_Media config rows or re-split jobs that were repointed. Back the database
    /// up before applying this in production.
    /// </summary>
    public partial class UnifyWhatsAppChannelType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Repoint jobs from each WP_Media config onto the surviving WP_Text config.
            //    Falls back to any WP_Text config if labels do not pair up as expected.
            migrationBuilder.Sql("""
                UPDATE dispatch_jobs dj
                SET channel_config_id = (
                    SELECT id FROM channel_configs
                    WHERE channel_type = 'WP_Text'
                    ORDER BY is_active DESC, created_at
                    LIMIT 1
                )
                WHERE dj.channel_config_id IN (
                    SELECT id FROM channel_configs WHERE channel_type = 'WP_Media'
                )
                AND EXISTS (SELECT 1 FROM channel_configs WHERE channel_type = 'WP_Text');
                """);

            // 2. Remove the now-unreferenced WP_Media configs. Guarded so that a database
            //    with only a WP_Media config (and no WP_Text) keeps it instead of ending up
            //    with no WhatsApp channel at all — it is renamed to WP in step 3.
            migrationBuilder.Sql("""
                DELETE FROM channel_configs
                WHERE channel_type = 'WP_Media'
                AND EXISTS (SELECT 1 FROM channel_configs WHERE channel_type = 'WP_Text');
                """);

            // 3. Collapse the stored type names.
            migrationBuilder.Sql("""
                UPDATE channel_configs   SET channel_type     = 'WP' WHERE channel_type     IN ('WP_Text','WP_Media');
                UPDATE dispatch_jobs     SET channel_type     = 'WP' WHERE channel_type     IN ('WP_Text','WP_Media');
                UPDATE fallback_policies SET trigger_channel  = 'WP' WHERE trigger_channel  IN ('WP_Text','WP_Media');
                UPDATE fallback_policies SET fallback_channel = 'WP' WHERE fallback_channel IN ('WP_Text','WP_Media');
                """);

            // 4. A WP_Text -> WP_Media rule is now WP -> WP; a channel cannot fall back to
            //    itself, so drop those rules rather than leave them to misfire.
            migrationBuilder.Sql(
                "DELETE FROM fallback_policies WHERE trigger_channel = fallback_channel;");

            // 5. message_requests.parsed_channels caches channel names as a jsonb array.
            //    Rewrite and de-duplicate ("WP_Text","WP_Media" both become "WP").
            migrationBuilder.Sql("""
                UPDATE message_requests
                SET parsed_channels = sub.rebuilt
                FROM (
                    SELECT m.id,
                           to_jsonb(ARRAY(
                               SELECT DISTINCT CASE WHEN e.v IN ('WP_Text','WP_Media') THEN 'WP' ELSE e.v END
                               FROM jsonb_array_elements_text(m.parsed_channels) AS e(v)
                           )) AS rebuilt
                    FROM message_requests m
                    WHERE m.parsed_channels @> '["WP_Text"]'::jsonb
                       OR m.parsed_channels @> '["WP_Media"]'::jsonb
                ) AS sub
                WHERE message_requests.id = sub.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Partial rollback only. WP becomes WP_Text again — the historical split between
            // text and media is unrecoverable, and the deleted WP_Media configs are gone.
            migrationBuilder.Sql("""
                UPDATE channel_configs   SET channel_type     = 'WP_Text' WHERE channel_type     = 'WP';
                UPDATE fallback_policies SET trigger_channel  = 'WP_Text' WHERE trigger_channel  = 'WP';
                UPDATE fallback_policies SET fallback_channel = 'WP_Text' WHERE fallback_channel = 'WP';
                """);

            // Jobs carrying a MediaUrl were media sends, so that much can be inferred back.
            migrationBuilder.Sql("""
                UPDATE dispatch_jobs SET channel_type = 'WP_Media'
                WHERE channel_type = 'WP' AND media_url IS NOT NULL;
                UPDATE dispatch_jobs SET channel_type = 'WP_Text'
                WHERE channel_type = 'WP';
                """);

            migrationBuilder.Sql("""
                UPDATE message_requests
                SET parsed_channels = sub.rebuilt
                FROM (
                    SELECT m.id,
                           to_jsonb(ARRAY(
                               SELECT DISTINCT CASE WHEN e.v = 'WP' THEN 'WP_Text' ELSE e.v END
                               FROM jsonb_array_elements_text(m.parsed_channels) AS e(v)
                           )) AS rebuilt
                    FROM message_requests m
                    WHERE m.parsed_channels @> '["WP"]'::jsonb
                ) AS sub
                WHERE message_requests.id = sub.id;
                """);
        }
    }
}

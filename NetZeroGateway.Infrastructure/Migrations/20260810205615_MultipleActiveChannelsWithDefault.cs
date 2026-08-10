using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <summary>
    /// Allows several active configs per channel type, with exactly one marked default.
    ///
    /// Ordering is load-bearing. The scaffolded version created the unique
    /// (channel_type, label_slug) index immediately after adding the column, when every
    /// existing row still had the empty-string default — so all rows of a type collided.
    /// Both columns are therefore backfilled before either index is created.
    /// </summary>
    public partial class MultipleActiveChannelsWithDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old index enforced one ACTIVE config per type — exactly what this
            // change removes. Drop it before inserting any second active row.
            migrationBuilder.DropIndex(
                name: "ix_channel_configs_channel_type_is_active",
                table: "channel_configs");

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "channel_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "label_slug",
                table: "channel_configs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Backfill slugs from labels: lowercase, strip everything but letters/digits
            // (hyphens included — the combined tag form splits on "-wp_label:"). Labels
            // that collapse to the same slug within a type get a numeric suffix, matching
            // ChannelConfigService.ResolveSlugAsync.
            migrationBuilder.Sql("""
                WITH slugged AS (
                    SELECT id,
                           channel_type,
                           NULLIF(regexp_replace(lower(label), '[^a-z0-9]', '', 'g'), '') AS base,
                           row_number() OVER (
                               PARTITION BY channel_type,
                                            NULLIF(regexp_replace(lower(label), '[^a-z0-9]', '', 'g'), '')
                               ORDER BY created_at, id
                           ) AS rn
                    FROM channel_configs
                )
                UPDATE channel_configs c
                SET label_slug = COALESCE(s.base, 'channel')
                               || CASE WHEN s.rn = 1 THEN '' ELSE s.rn::text END
                FROM slugged s
                WHERE c.id = s.id;
                """);

            // Promote one config per type to default: the active one, oldest first when
            // several qualify. Without this no type would have a default and every
            // untagged send would fail to resolve a channel.
            migrationBuilder.Sql("""
                UPDATE channel_configs c
                SET is_default = true
                FROM (
                    SELECT DISTINCT ON (channel_type) id
                    FROM channel_configs
                    ORDER BY channel_type, is_active DESC, created_at, id
                ) pick
                WHERE c.id = pick.id;
                """);

            // A default must be usable.
            migrationBuilder.Sql(
                "UPDATE channel_configs SET is_active = true WHERE is_default AND NOT is_active;");

            migrationBuilder.CreateIndex(
                name: "ix_channel_configs_channel_type_is_default",
                table: "channel_configs",
                columns: new[] { "channel_type", "is_default" },
                unique: true,
                filter: "\"is_default\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_channel_configs_channel_type_label_slug",
                table: "channel_configs",
                columns: new[] { "channel_type", "label_slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_channel_configs_channel_type_is_default",
                table: "channel_configs");

            migrationBuilder.DropIndex(
                name: "ix_channel_configs_channel_type_label_slug",
                table: "channel_configs");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "channel_configs");

            migrationBuilder.DropColumn(
                name: "label_slug",
                table: "channel_configs");

            // Restoring one-active-per-type would fail outright if the operator has since
            // activated several configs of a type, so stand the extras down first. Which
            // one survives is arbitrary (oldest wins) — the previous default is not
            // recoverable once the column is gone.
            migrationBuilder.Sql("""
                UPDATE channel_configs c
                SET is_active = false
                WHERE c.is_active
                  AND c.id <> (
                      SELECT id FROM channel_configs x
                      WHERE x.channel_type = c.channel_type AND x.is_active
                      ORDER BY created_at, id
                      LIMIT 1
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "ix_channel_configs_channel_type_is_active",
                table: "channel_configs",
                columns: new[] { "channel_type", "is_active" },
                unique: true,
                filter: "\"is_active\" = true");
        }
    }
}

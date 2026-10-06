using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <summary>
    /// Hotfix batch 9, Tasks 5/6: the provider catalog rename
    /// (NetZeroInbox/Qr/Template/Voice -> WA Meta/WA Devices QR/WA Meta Template/Net ZerO
    /// call Boardcast) only ever reached the Add/Edit dropdown and brand-new channels —
    /// Seed:ChannelConfigs looks existing rows up by (channel_type, label), not by provider,
    /// so renaming provider_name in config does nothing for a channel that already exists.
    /// Renames provider_name on the 4 rows still carrying the exact old default text, the
    /// same strings the catalog used to prefill with. A row an admin has since edited no
    /// longer matches that exact string and is left alone; re-running this after an admin
    /// edit (or a second deploy) touches zero rows either way — same idempotence pattern
    /// as 20260920174820_BackfillDispatchJobRecipients.
    /// </summary>
    public partial class RenameProviderNamesForTask9 : Migration
    {
        /// <summary>
        /// Confirmed by the team lead via Seed:ChannelConfigs: "Boardcast", not "Broadcast" —
        /// matches the frontend catalog's label in config-form-dialog.component.ts.
        /// </summary>
        private const string VoiceProviderName = "Net ZerO call Boardcast";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE channel_configs SET provider_name = 'WA Meta'
                WHERE provider = 'NetZeroInbox' AND provider_name = 'NetZero Inbox WhatsApp (v1)';

                UPDATE channel_configs SET provider_name = 'WA Devices QR'
                WHERE provider = 'NetZeroInboxQr' AND provider_name = 'NetZero Inbox WhatsApp (QR)';

                UPDATE channel_configs SET provider_name = 'WA Meta Template'
                WHERE provider = 'NetZeroInboxTemplate' AND provider_name = 'NetZero Inbox Template';

                UPDATE channel_configs SET provider_name = '{VoiceProviderName}'
                WHERE provider = 'NetZeroVoice' AND provider_name = 'Netzero Voice Provider';
                """);
        }

        /// <summary>
        /// Forward-fix only, per release-process.md — schema (and seeded data) is never
        /// rolled back in place.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

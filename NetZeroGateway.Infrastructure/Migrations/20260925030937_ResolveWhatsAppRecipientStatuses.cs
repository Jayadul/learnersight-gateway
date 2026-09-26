using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResolveWhatsAppRecipientStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No WhatsApp provider has a delivery lookup, so rows written as Pending before
            // this change waited for a sync that never runs for them. Their job was accepted
            // (Sent), which is exactly the answer new WhatsApp rows now take at send time.
            // Idempotent: only touches rows still Pending.
            migrationBuilder.Sql(@"
UPDATE dispatch_job_recipients r
SET delivery_status = 'Delivered', updated_at = now()
FROM dispatch_jobs j
WHERE j.id = r.dispatch_job_id
  AND j.channel_type = 'WP'
  AND j.status = 'Sent'
  AND r.delivery_status = 'Pending';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only and not reversible: the previous Pending value carried no information.
        }
    }
}

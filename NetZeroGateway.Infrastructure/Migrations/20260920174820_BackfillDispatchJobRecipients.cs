using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillDispatchJobRecipients : Migration
    {
        /// <summary>
        /// dispatch_job_recipients (added 2026-09-05) was never backfilled for jobs sent
        /// before it existed, so any dispatch_jobs row older than that has no matching
        /// recipient rows — the new per-recipient Message Logs page would silently drop
        /// pre-R3 history. This reconstructs one recipient row per number in the job's
        /// comma-joined to_number, one time only (hotfix batch 3, task 3).
        /// <para>
        /// True per-recipient outcomes were never recorded for these, so status is only
        /// the best available guess — the job's own status, mapped across (Pending stays
        /// Pending, Sent becomes Delivered, Failed stays Failed). They are marked distinctly
        /// via provider_reference_id = 'backfilled' rather than left indistinguishable from
        /// a genuine provider reference, so nobody mistakes a guess for confirmed delivery.
        /// </para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO dispatch_job_recipients
                    (id, dispatch_job_id, recipient, provider_reference_id, delivery_status,
                     raw_status_response, last_checked_at, created_at, updated_at)
                SELECT
                    gen_random_uuid(),
                    j.id,
                    btrim(num),
                    'backfilled',
                    CASE j.status
                        WHEN 'Sent'   THEN 'Delivered'
                        WHEN 'Failed' THEN 'Failed'
                        ELSE 'Pending'
                    END,
                    NULL,
                    NULL,
                    j.created_at,
                    j.created_at
                FROM dispatch_jobs j
                CROSS JOIN LATERAL unnest(string_to_array(j.to_number, ',')) AS num
                WHERE btrim(num) <> ''
                  AND NOT EXISTS (
                      SELECT 1 FROM dispatch_job_recipients r
                      WHERE r.dispatch_job_id = j.id
                  );");
        }

        /// <summary>
        /// Forward-fix only, per release-process.md — schema is never rolled back in place.
        /// Rows this migration created are only identifiable as a group by re-running the
        /// same join, which risks catching genuine rows in the (currently theoretical) case
        /// of a real provider ever returning the literal reference id "backfilled" — not
        /// worth the risk for a Down path nothing in the ship process calls.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGenNetProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GenNet is retired (folded into Chithi — same vendor account). No schema
            // change: Provider is stored as a string column, but the app can no longer
            // deserialize a "GenNet" row once the enum member is gone, so every trace of
            // it — recipients, jobs, then the channel config itself — is removed here.
            // Team-lead-approved: this discards the handful of historical GenNet sends
            // rather than retiring the channel in place.
            migrationBuilder.Sql("""
                DELETE FROM dispatch_job_recipients
                USING dispatch_jobs, channel_configs
                WHERE dispatch_job_recipients.dispatch_job_id = dispatch_jobs.id
                  AND dispatch_jobs.channel_config_id = channel_configs.id
                  AND channel_configs.provider = 'GenNet';
                """);

            migrationBuilder.Sql("""
                DELETE FROM dispatch_jobs
                USING channel_configs
                WHERE dispatch_jobs.channel_config_id = channel_configs.id
                  AND channel_configs.provider = 'GenNet';
                """);

            migrationBuilder.Sql("""
                DELETE FROM fallback_policies
                WHERE trigger_channel_config_id IN
                    (SELECT id FROM channel_configs WHERE provider = 'GenNet')
                   OR fallback_channel_config_id IN
                    (SELECT id FROM channel_configs WHERE provider = 'GenNet');
                """);

            migrationBuilder.Sql("DELETE FROM channel_configs WHERE provider = 'GenNet';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the deleted rows (and the enum member the app needs to read
            // them back) are gone. Restoring GenNet is a forward migration, not a rollback.
        }
    }
}

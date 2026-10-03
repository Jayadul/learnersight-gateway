using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only fix for hotfix batch 9, Task 9: is_global was an admin-set flag
    /// independent of trigger_channel_config_id, defaulted off by the UI for a new
    /// policy regardless of scope — so a policy correctly scoped to one channel could
    /// silently never be found by fallback resolution (which, as of this same hotfix,
    /// no longer filters on is_global at all; this just brings already-stored rows'
    /// displayed value back in line with what they actually mean).
    /// </summary>
    public partial class FixFallbackPolicyIsGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE fallback_policies SET is_global = (trigger_channel_config_id IS NULL);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only; the pre-fix values are not recoverable and not worth keeping.
        }
    }
}

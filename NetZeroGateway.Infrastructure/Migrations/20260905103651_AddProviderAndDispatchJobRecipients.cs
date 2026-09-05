using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAndDispatchJobRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "channel_configs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Backfill every existing channel. The column stores the enum as text, and the
            // "" the generator defaults to parses back to nothing — leaving it would make
            // every pre-R3 channel unreadable the moment the app queried it. Until this
            // release a channel type had exactly one provider, so the mapping is exact.
            migrationBuilder.Sql(@"
                UPDATE channel_configs
                SET provider = CASE channel_type
                    WHEN 'SMS'   THEN 'Reve'
                    WHEN 'WP'    THEN 'WaTrend'
                    WHEN 'Voice' THEN 'NetZeroVoice'
                    ELSE 'Reve'
                END
                WHERE provider IS NULL OR provider = '';");

            migrationBuilder.CreateTable(
                name: "dispatch_job_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_reference_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    raw_status_response = table.Column<string>(type: "jsonb", nullable: true),
                    last_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatch_job_recipients", x => x.id);
                    table.ForeignKey(
                        name: "fk_dispatch_job_recipients_dispatch_jobs_dispatch_job_id",
                        column: x => x.dispatch_job_id,
                        principalTable: "dispatch_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_job_recipients_delivery_status",
                table: "dispatch_job_recipients",
                column: "delivery_status");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_job_recipients_dispatch_job_id",
                table: "dispatch_job_recipients",
                column: "dispatch_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_job_recipients_provider_reference_id",
                table: "dispatch_job_recipients",
                column: "provider_reference_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_job_recipients_recipient",
                table: "dispatch_job_recipients",
                column: "recipient");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_job_recipients");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "channel_configs");
        }
    }
}

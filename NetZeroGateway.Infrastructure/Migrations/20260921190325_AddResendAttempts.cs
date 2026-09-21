using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResendAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resend_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_job_recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_reference_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    raw_response = table.Column<string>(type: "jsonb", nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resend_attempts", x => x.id);
                    table.ForeignKey(
                        name: "fk_resend_attempts_dispatch_job_recipients_dispatch_job_recipi",
                        column: x => x.dispatch_job_recipient_id,
                        principalTable: "dispatch_job_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resend_attempts_dispatch_jobs_dispatch_job_id",
                        column: x => x.dispatch_job_id,
                        principalTable: "dispatch_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resend_attempts_dispatch_job_id",
                table: "resend_attempts",
                column: "dispatch_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_resend_attempts_dispatch_job_recipient_id",
                table: "resend_attempts",
                column: "dispatch_job_recipient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resend_attempts");
        }
    }
}

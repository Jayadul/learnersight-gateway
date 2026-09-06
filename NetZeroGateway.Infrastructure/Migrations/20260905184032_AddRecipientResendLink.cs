using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipientResendLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "resent_from_recipient_id",
                table: "dispatch_job_recipients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_job_recipients_resent_from_recipient_id",
                table: "dispatch_job_recipients",
                column: "resent_from_recipient_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dispatch_job_recipients_resent_from_recipient_id",
                table: "dispatch_job_recipients");

            migrationBuilder.DropColumn(
                name: "resent_from_recipient_id",
                table: "dispatch_job_recipients");
        }
    }
}

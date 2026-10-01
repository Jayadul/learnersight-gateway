using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoiceCallTimingToRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "call_connect_time",
                table: "dispatch_job_recipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "call_end_time",
                table: "dispatch_job_recipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "call_start_time",
                table: "dispatch_job_recipients",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "call_connect_time",
                table: "dispatch_job_recipients");

            migrationBuilder.DropColumn(
                name: "call_end_time",
                table: "dispatch_job_recipients");

            migrationBuilder.DropColumn(
                name: "call_start_time",
                table: "dispatch_job_recipients");
        }
    }
}

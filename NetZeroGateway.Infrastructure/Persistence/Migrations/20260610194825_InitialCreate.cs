using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "api_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    to_user = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    raw_body = table.Column<string>(type: "text", nullable: false),
                    response_body = table.Column<string>(type: "text", nullable: false),
                    caller_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    response_status = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    message_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "channel_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    config_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fallback_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trigger_channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fallback_channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_global = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fallback_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    caller_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    to_user = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    raw_message_content = table.Column<string>(type: "text", nullable: false),
                    parsed_channels = table.Column<string>(type: "jsonb", nullable: false),
                    overall_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "x_soft_api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    secret_key_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_x_soft_api_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "x_soft_app_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    shared_secret_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_x_soft_app_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    channel_config_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_content = table.Column<string>(type: "text", nullable: false),
                    media_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    campaign_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    to_number = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    provider_raw_response = table.Column<string>(type: "text", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_fallback = table.Column<bool>(type: "boolean", nullable: false),
                    fallback_from_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispatch_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_dispatch_jobs_channel_configs_channel_config_id",
                        column: x => x.channel_config_id,
                        principalTable: "channel_configs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispatch_jobs_message_requests_message_request_id",
                        column: x => x.message_request_id,
                        principalTable: "message_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_username",
                table: "admin_users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_logs_requested_at",
                table: "api_logs",
                column: "requested_at");

            migrationBuilder.CreateIndex(
                name: "ix_api_logs_response_status",
                table: "api_logs",
                column: "response_status");

            migrationBuilder.CreateIndex(
                name: "ix_channel_configs_channel_type_is_active",
                table: "channel_configs",
                columns: new[] { "channel_type", "is_active" },
                unique: true,
                filter: "\"is_active\" = true");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_jobs_channel_config_id",
                table: "dispatch_jobs",
                column: "channel_config_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_jobs_created_at",
                table: "dispatch_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_jobs_message_request_id",
                table: "dispatch_jobs",
                column: "message_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispatch_jobs_status",
                table: "dispatch_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_message_requests_overall_status",
                table: "message_requests",
                column: "overall_status");

            migrationBuilder.CreateIndex(
                name: "ix_message_requests_received_at",
                table: "message_requests",
                column: "received_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "api_logs");

            migrationBuilder.DropTable(
                name: "dispatch_jobs");

            migrationBuilder.DropTable(
                name: "fallback_policies");

            migrationBuilder.DropTable(
                name: "x_soft_api_keys");

            migrationBuilder.DropTable(
                name: "x_soft_app_configs");

            migrationBuilder.DropTable(
                name: "channel_configs");

            migrationBuilder.DropTable(
                name: "message_requests");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetZeroGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiLogGatewayResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "gateway_result",
                table: "api_logs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Backfill from response_body, which is always the controlled SendTextResponse
            // shape for this table — no reason to have it unset just because a row
            // predates this column.
            migrationBuilder.Sql(
                "UPDATE api_logs SET gateway_result = (response_body::json->>'Text') "
                + "WHERE response_body LIKE '{%';");

            migrationBuilder.CreateIndex(
                name: "ix_api_logs_gateway_result",
                table: "api_logs",
                column: "gateway_result");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_api_logs_gateway_result",
                table: "api_logs");

            migrationBuilder.DropColumn(
                name: "gateway_result",
                table: "api_logs");
        }
    }
}

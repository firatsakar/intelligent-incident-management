using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelemetryIngestionService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeUniqueIndexesToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telemetry_sources_Name",
                table: "telemetry_sources");

            migrationBuilder.DropIndex(
                name: "IX_log_records_Fingerprint_Timestamp",
                table: "log_records");

            migrationBuilder.DropIndex(
                name: "IX_error_signatures_Fingerprint",
                table: "error_signatures");

            migrationBuilder.DropIndex(
                name: "IX_detection_rules_Name",
                table: "detection_rules");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_sources_OrganizationId_Name",
                table: "telemetry_sources",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_log_records_OrganizationId_Fingerprint_Timestamp",
                table: "log_records",
                columns: new[] { "OrganizationId", "Fingerprint", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_error_signatures_OrganizationId_Fingerprint",
                table: "error_signatures",
                columns: new[] { "OrganizationId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detection_rules_OrganizationId_Name",
                table: "detection_rules",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telemetry_sources_OrganizationId_Name",
                table: "telemetry_sources");

            migrationBuilder.DropIndex(
                name: "IX_log_records_OrganizationId_Fingerprint_Timestamp",
                table: "log_records");

            migrationBuilder.DropIndex(
                name: "IX_error_signatures_OrganizationId_Fingerprint",
                table: "error_signatures");

            migrationBuilder.DropIndex(
                name: "IX_detection_rules_OrganizationId_Name",
                table: "detection_rules");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_sources_Name",
                table: "telemetry_sources",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_log_records_Fingerprint_Timestamp",
                table: "log_records",
                columns: new[] { "Fingerprint", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_error_signatures_Fingerprint",
                table: "error_signatures",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_detection_rules_Name",
                table: "detection_rules",
                column: "Name",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelemetryIngestionService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationToTelemetryRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "source_cursors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "signals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "log_records",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "error_signatures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "detection_rules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_source_cursors_OrganizationId",
                table: "source_cursors",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_signals_OrganizationId",
                table: "signals",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_log_records_OrganizationId",
                table: "log_records",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_error_signatures_OrganizationId",
                table: "error_signatures",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_detection_rules_OrganizationId",
                table: "detection_rules",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_source_cursors_OrganizationId",
                table: "source_cursors");

            migrationBuilder.DropIndex(
                name: "IX_signals_OrganizationId",
                table: "signals");

            migrationBuilder.DropIndex(
                name: "IX_log_records_OrganizationId",
                table: "log_records");

            migrationBuilder.DropIndex(
                name: "IX_error_signatures_OrganizationId",
                table: "error_signatures");

            migrationBuilder.DropIndex(
                name: "IX_detection_rules_OrganizationId",
                table: "detection_rules");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "source_cursors");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "signals");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "log_records");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "error_signatures");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "detection_rules");
        }
    }
}

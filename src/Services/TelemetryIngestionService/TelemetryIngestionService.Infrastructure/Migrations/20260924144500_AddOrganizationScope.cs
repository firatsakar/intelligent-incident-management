using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelemetryIngestionService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "telemetry_sources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "outbox_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_sources_OrganizationId",
                table: "telemetry_sources",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telemetry_sources_OrganizationId",
                table: "telemetry_sources");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "telemetry_sources");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "outbox_messages");
        }
    }
}

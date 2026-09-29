using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentApiKeysAndExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "incidents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedBy",
                table: "incidents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "incident_api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_api_keys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_open_external_id",
                table: "incidents",
                columns: new[] { "OrganizationId", "ExternalId" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL AND \"Status\" IN ('Open', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_incident_api_keys_KeyHash",
                table: "incident_api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_incident_api_keys_OrganizationId_Name",
                table: "incident_api_keys",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_api_keys");

            migrationBuilder.DropIndex(
                name: "IX_incidents_open_external_id",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "ReportedBy",
                table: "incidents");
        }
    }
}

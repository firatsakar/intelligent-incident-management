using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelemetryIngestionService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIngestKeyToTelemetrySources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IngestKeyHash",
                table: "telemetry_sources",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngestKeyPrefix",
                table: "telemetry_sources",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_sources_IngestKeyHash",
                table: "telemetry_sources",
                column: "IngestKeyHash",
                unique: true,
                filter: "\"IngestKeyHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telemetry_sources_IngestKeyHash",
                table: "telemetry_sources");

            migrationBuilder.DropColumn(
                name: "IngestKeyHash",
                table: "telemetry_sources");

            migrationBuilder.DropColumn(
                name: "IngestKeyPrefix",
                table: "telemetry_sources");
        }
    }
}

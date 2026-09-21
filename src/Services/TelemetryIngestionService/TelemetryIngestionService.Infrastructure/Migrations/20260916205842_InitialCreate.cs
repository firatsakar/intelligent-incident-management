using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelemetryIngestionService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "detection_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Service = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MinSeverity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    DedupWindowHours = table.Column<int>(type: "integer", nullable: false),
                    PromoteThreshold = table.Column<double>(type: "double precision", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detection_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "error_signatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Service = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    NormalizedMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccurrenceCount = table.Column<long>(type: "bigint", nullable: false),
                    CurrentIncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastPromotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentIncidentOccurrences = table.Column<long>(type: "bigint", nullable: false),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    PromotionCount = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedRealCount = table.Column<int>(type: "integer", nullable: false),
                    FalsePositiveCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_error_signatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "log_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetrySourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Service = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Message = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    NormalizedMessage = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    ExceptionType = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    StackTrace = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IngestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasClockSkew = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "signals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ErrorSignatureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccurrenceCount = table.Column<long>(type: "bigint", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    score_breakdown = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "source_cursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetrySourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LastEventTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_cursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PollIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_sources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_detection_rules_Name",
                table: "detection_rules",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_error_signatures_CurrentIncidentId",
                table: "error_signatures",
                column: "CurrentIncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_error_signatures_Fingerprint",
                table: "error_signatures",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_log_records_Fingerprint_Timestamp",
                table: "log_records",
                columns: new[] { "Fingerprint", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_log_records_TelemetrySourceId_SourceEventId",
                table: "log_records",
                columns: new[] { "TelemetrySourceId", "SourceEventId" },
                unique: true,
                filter: "\"SourceEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_log_records_Timestamp",
                table: "log_records",
                column: "Timestamp")
                .Annotation("Npgsql:IndexMethod", "brin");

            migrationBuilder.CreateIndex(
                name: "IX_signals_DetectedAt",
                table: "signals",
                column: "DetectedAt")
                .Annotation("Npgsql:IndexMethod", "brin");

            migrationBuilder.CreateIndex(
                name: "IX_signals_ErrorSignatureId",
                table: "signals",
                column: "ErrorSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_signals_Status",
                table: "signals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_source_cursors_TelemetrySourceId",
                table: "source_cursors",
                column: "TelemetrySourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_sources_Name",
                table: "telemetry_sources",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detection_rules");

            migrationBuilder.DropTable(
                name: "error_signatures");

            migrationBuilder.DropTable(
                name: "log_records");

            migrationBuilder.DropTable(
                name: "signals");

            migrationBuilder.DropTable(
                name: "source_cursors");

            migrationBuilder.DropTable(
                name: "telemetry_sources");
        }
    }
}

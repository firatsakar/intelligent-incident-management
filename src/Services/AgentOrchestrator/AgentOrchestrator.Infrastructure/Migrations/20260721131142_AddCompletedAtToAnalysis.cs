using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentOrchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedAtToAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "incident_analyses",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "incident_analyses");
        }
    }
}

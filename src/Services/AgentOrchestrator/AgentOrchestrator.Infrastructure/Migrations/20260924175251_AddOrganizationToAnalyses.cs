using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentOrchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationToAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "incident_analyses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_incident_analyses_OrganizationId",
                table: "incident_analyses",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incident_analyses_OrganizationId",
                table: "incident_analyses");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "incident_analyses");
        }
    }
}

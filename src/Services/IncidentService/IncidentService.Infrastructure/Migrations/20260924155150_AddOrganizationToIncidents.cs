using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationToIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "incidents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_incidents_OrganizationId_CreatedAt",
                table: "incidents",
                columns: new[] { "OrganizationId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incidents_OrganizationId_CreatedAt",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "incidents");
        }
    }
}

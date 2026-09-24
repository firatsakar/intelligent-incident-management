using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationToIntegrationsAndDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_integrations_Name",
                table: "integrations");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "notification_deliveries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "integrations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_OrganizationId_CreatedAt",
                table: "notification_deliveries",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integrations_OrganizationId_Name",
                table: "integrations",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_deliveries_OrganizationId_CreatedAt",
                table: "notification_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_integrations_OrganizationId_Name",
                table: "integrations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "integrations");

            migrationBuilder.CreateIndex(
                name: "IX_integrations_Name",
                table: "integrations",
                column: "Name",
                unique: true);
        }
    }
}

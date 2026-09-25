using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationNameToDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "notification_deliveries",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntegrationName",
                table: "notification_deliveries",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            // Rows written before this column existed learn their integration's name and channel
            // from the integration, where it still exists. A delivery whose integration was
            // deleted keeps nulls, which the console already words as a deleted integration.
            migrationBuilder.Sql(
                """
                UPDATE notification_deliveries AS d
                SET "IntegrationName" = i."Name", "Channel" = i."Channel"
                FROM integrations AS i
                WHERE i."Id" = d."IntegrationId";
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channel",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "IntegrationName",
                table: "notification_deliveries");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentOrchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ParkedAt",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "ParkedAt",
                table: "outbox_messages");
        }
    }
}

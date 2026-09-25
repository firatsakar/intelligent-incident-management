using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentOrchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "github_connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    repositories = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_github_connections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_github_connections_OrganizationId",
                table: "github_connections",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "github_connections");
        }
    }
}

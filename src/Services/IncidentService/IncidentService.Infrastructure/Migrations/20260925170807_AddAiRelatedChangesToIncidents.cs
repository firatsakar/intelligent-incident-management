using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiRelatedChangesToIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ai_related_changes",
                table: "incidents",
                type: "jsonb",
                nullable: false,
                // "[]", not the "" EF generates for a string column: existing rows need valid
                // JSON, and an empty string is not.
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ai_related_changes",
                table: "incidents");
        }
    }
}

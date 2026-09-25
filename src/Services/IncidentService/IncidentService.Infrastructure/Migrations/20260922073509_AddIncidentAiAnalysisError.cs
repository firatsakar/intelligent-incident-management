using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentAiAnalysisError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiAnalysisError",
                table: "incidents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAnalysisError",
                table: "incidents");
        }
    }
}

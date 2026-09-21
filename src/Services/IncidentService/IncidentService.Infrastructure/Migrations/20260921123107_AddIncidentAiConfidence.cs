using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentAiConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AiConfidence",
                table: "incidents",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiConfidence",
                table: "incidents");
        }
    }
}

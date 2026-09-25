using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IncidentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIncidentVerdict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "incidents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Verdict",
                table: "incidents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "Verdict",
                table: "incidents");
        }
    }
}

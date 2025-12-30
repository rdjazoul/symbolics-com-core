using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIgdbRefreshData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IgdbId",
                table: "Game",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttempt",
                table: "GameEnrichmentQueue",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IgdbId",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "LastAttempt",
                table: "GameEnrichmentQueue");
        }
    }
}

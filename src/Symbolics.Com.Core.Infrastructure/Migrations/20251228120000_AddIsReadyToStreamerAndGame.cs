using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    public partial class AddIsReadyToStreamerAndGame : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReady",
                table: "Streamer",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReady",
                table: "Game",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Streamer_IsReady",
                table: "Streamer",
                column: "Id",
                filter: "\"IsReady\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Game_IsReady",
                table: "Game",
                column: "Id",
                filter: "\"IsReady\" = TRUE");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Streamer_IsReady",
                table: "Streamer");

            migrationBuilder.DropIndex(
                name: "IX_Game_IsReady",
                table: "Game");

            migrationBuilder.DropColumn(
                name: "IsReady",
                table: "Streamer");

            migrationBuilder.DropColumn(
                name: "IsReady",
                table: "Game");
        }
    }
}

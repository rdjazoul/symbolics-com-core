using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTwitchStreamIdToGamePlayed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwitchName",
                table: "StreamerTwitch",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TwitchStreamId",
                table: "GamePlayed",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayed_TwitchStreamId",
                table: "GamePlayed",
                column: "TwitchStreamId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GamePlayed_TwitchStreamId",
                table: "GamePlayed");

            migrationBuilder.DropColumn(
                name: "TwitchName",
                table: "StreamerTwitch");

            migrationBuilder.DropColumn(
                name: "TwitchStreamId",
                table: "GamePlayed");
        }
    }
}

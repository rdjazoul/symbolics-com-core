using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamePlayedStreamerLanguageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_GamePlayed_StreamerId_Language",
                table: "GamePlayed",
                columns: new[] { "StreamerId", "Language" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GamePlayed_StreamerId_Language",
                table: "GamePlayed");
        }
    }
}

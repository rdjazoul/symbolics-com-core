using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageToStreamer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Streamer",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "Streamer");
        }
    }
}

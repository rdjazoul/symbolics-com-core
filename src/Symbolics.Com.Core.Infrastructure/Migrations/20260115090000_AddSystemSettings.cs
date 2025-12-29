using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Symbolics.Com.Core.Infrastructure.Persistence;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations;

[DbContext(typeof(CoreDbContext))]
[Migration("20260115090000_AddSystemSettings")]
public partial class AddSystemSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SystemSettings",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemSettings", x => x.Key);
            });

        migrationBuilder.InsertData(
            table: "SystemSettings",
            columns: new[] { "Key", "Value" },
            values: new object[] { "RecommendationTopGamesLimit", "500" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SystemSettings");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    public partial class AddWorkerStateIsEnabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "WorkerState",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE "WorkerState"
                SET "WorkerName" = 'TwitchDiscovery'
                WHERE "WorkerName" = 'TwitchCrawler';
                """);

            migrationBuilder.Sql("""
                INSERT INTO "WorkerState" ("WorkerName", "CurrentCursor", "LastCleanupDate", "IsEnabled")
                SELECT 'TwitchDiscovery', NULL, NOW(), TRUE
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "WorkerState"
                    WHERE "WorkerName" = 'TwitchDiscovery'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "WorkerState" ("WorkerName", "CurrentCursor", "LastCleanupDate", "IsEnabled")
                SELECT 'Enrichment', NULL, NOW(), TRUE
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "WorkerState"
                    WHERE "WorkerName" = 'Enrichment'
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "WorkerState"
                WHERE "WorkerName" = 'Enrichment';
                """);

            migrationBuilder.Sql("""
                UPDATE "WorkerState"
                SET "WorkerName" = 'TwitchCrawler'
                WHERE "WorkerName" = 'TwitchDiscovery';
                """);

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "WorkerState");
        }
    }
}

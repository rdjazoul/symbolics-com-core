using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations
{
    public class UpdateStreamerLanguagesToArray : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Language",
                table: "Streamer",
                newName: "Languages");

            migrationBuilder.Sql(
                """
                ALTER TABLE "Streamer"
                ALTER COLUMN "Languages"
                TYPE text[]
                USING CASE
                    WHEN "Languages" IS NULL THEN NULL
                    ELSE ARRAY["Languages"]
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "Streamer"
                ALTER COLUMN "Languages"
                TYPE character varying(20)
                USING CASE
                    WHEN "Languages" IS NULL THEN NULL
                    ELSE "Languages"[1]
                END;
                """);

            migrationBuilder.RenameColumn(
                name: "Languages",
                table: "Streamer",
                newName: "Language");
        }
    }
}

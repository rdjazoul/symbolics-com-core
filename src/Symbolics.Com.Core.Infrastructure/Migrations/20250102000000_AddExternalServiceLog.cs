using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Symbolics.Com.Core.Infrastructure.Persistence;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations;

[DbContext(typeof(CoreDbContext))]
[Migration("20250102000000_AddExternalServiceLog")]
public partial class AddExternalServiceLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExternalServiceLog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Service = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ActionType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                InputUnits = table.Column<long>(type: "bigint", nullable: false),
                OutputUnits = table.Column<long>(type: "bigint", nullable: false),
                ExecutionTimeMs = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalServiceLog", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExternalServiceLog_CreatedAt",
            table: "ExternalServiceLog",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ExternalServiceLog_Service_ActionType",
            table: "ExternalServiceLog",
            columns: new[] { "Service", "ActionType" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ExternalServiceLog");
    }
}

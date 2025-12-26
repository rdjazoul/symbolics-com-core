using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Symbolics.Com.Core.Infrastructure.Persistence;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations;

[DbContext(typeof(CoreDbContext))]
[Migration("20250101000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Campaign",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserDescription = table.Column<string>(type: "text", nullable: true),
                OptimizedDescription = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Campaign", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Game",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                VectorDescription = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Game", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Streamer",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                VectorDescription = table.Column<string>(type: "text", nullable: true),
                PersonaDescription = table.Column<string>(type: "text", nullable: true),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Streamer", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WorkerState",
            columns: table => new
            {
                WorkerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CurrentCursor = table.Column<string>(type: "text", nullable: true),
                LastCleanupDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkerState", x => x.WorkerName);
            });

        migrationBuilder.CreateTable(
            name: "GameEnrichmentQueue",
            columns: table => new
            {
                GameId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                RetryCount = table.Column<int>(type: "integer", nullable: false),
                AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameEnrichmentQueue", x => x.GameId);
                table.ForeignKey(
                    name: "FK_GameEnrichmentQueue_Game_GameId",
                    column: x => x.GameId,
                    principalTable: "Game",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GamePlayed",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StreamerId = table.Column<Guid>(type: "uuid", nullable: false),
                GameId = table.Column<Guid>(type: "uuid", nullable: false),
                ViewerCount = table.Column<int>(type: "integer", nullable: false),
                Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GamePlayed", x => x.Id);
                table.ForeignKey(
                    name: "FK_GamePlayed_Game_GameId",
                    column: x => x.GameId,
                    principalTable: "Game",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GamePlayed_Streamer_StreamerId",
                    column: x => x.StreamerId,
                    principalTable: "Streamer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GameTwitch",
            columns: table => new
            {
                TwitchId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                GameId = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameTwitch", x => x.TwitchId);
                table.ForeignKey(
                    name: "FK_GameTwitch_Game_GameId",
                    column: x => x.GameId,
                    principalTable: "Game",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StreamerEnrichmentQueue",
            columns: table => new
            {
                StreamerId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                RetryCount = table.Column<int>(type: "integer", nullable: false),
                AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamerEnrichmentQueue", x => x.StreamerId);
                table.ForeignKey(
                    name: "FK_StreamerEnrichmentQueue_Streamer_StreamerId",
                    column: x => x.StreamerId,
                    principalTable: "Streamer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StreamerTwitch",
            columns: table => new
            {
                TwitchId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                StreamerId = table.Column<Guid>(type: "uuid", nullable: false),
                TwitchLogin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamerTwitch", x => x.TwitchId);
                table.ForeignKey(
                    name: "FK_StreamerTwitch_Streamer_StreamerId",
                    column: x => x.StreamerId,
                    principalTable: "Streamer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StreamerYoutube",
            columns: table => new
            {
                StreamerId = table.Column<Guid>(type: "uuid", nullable: false),
                YoutubeUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StreamerYoutube", x => x.StreamerId);
                table.ForeignKey(
                    name: "FK_StreamerYoutube_Streamer_StreamerId",
                    column: x => x.StreamerId,
                    principalTable: "Streamer",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql(
            "INSERT INTO \"WorkerState\" (\"WorkerName\", \"CurrentCursor\", \"LastCleanupDate\") VALUES ('TwitchCrawler', NULL, NOW());");

        migrationBuilder.CreateIndex(
            name: "IX_GamePlayed_Date",
            table: "GamePlayed",
            column: "Date");

        migrationBuilder.CreateIndex(
            name: "IX_GamePlayed_GameId",
            table: "GamePlayed",
            column: "GameId");

        migrationBuilder.CreateIndex(
            name: "IX_GamePlayed_StreamerId_GameId_Date",
            table: "GamePlayed",
            columns: new[] { "StreamerId", "GameId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_GameTwitch_GameId",
            table: "GameTwitch",
            column: "GameId");

        migrationBuilder.CreateIndex(
            name: "IX_Streamer_Email",
            table: "Streamer",
            column: "Email");

        migrationBuilder.CreateIndex(
            name: "IX_StreamerTwitch_StreamerId",
            table: "StreamerTwitch",
            column: "StreamerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Campaign");
        migrationBuilder.DropTable(name: "GameEnrichmentQueue");
        migrationBuilder.DropTable(name: "GamePlayed");
        migrationBuilder.DropTable(name: "GameTwitch");
        migrationBuilder.DropTable(name: "StreamerEnrichmentQueue");
        migrationBuilder.DropTable(name: "StreamerTwitch");
        migrationBuilder.DropTable(name: "StreamerYoutube");
        migrationBuilder.DropTable(name: "WorkerState");
        migrationBuilder.DropTable(name: "Game");
        migrationBuilder.DropTable(name: "Streamer");
    }
}

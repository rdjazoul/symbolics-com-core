using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Symbolics.Com.Core.Infrastructure.Persistence;

#nullable disable

namespace Symbolics.Com.Core.Infrastructure.Migrations;

[DbContext(typeof(CoreDbContext))]
public partial class CoreDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.0");

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.Campaign", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<string>("OptimizedDescription")
                .HasColumnType("text");

            b.Property<Guid>("UserId")
                .HasColumnType("uuid");

            b.Property<string>("UserDescription")
                .HasColumnType("text");

            b.HasKey("Id");

            b.ToTable("Campaign");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.ExternalServiceLog", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<string>("ActionType")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("ExecutionTimeMs")
                .HasColumnType("integer");

            b.Property<long>("InputUnits")
                .HasColumnType("bigint");

            b.Property<string>("Model")
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<long>("OutputUnits")
                .HasColumnType("bigint");

            b.Property<string>("Service")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.HasKey("Id");

            b.HasIndex("CreatedAt");

            b.HasIndex("Service", "ActionType");

            b.ToTable("ExternalServiceLog");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.Game", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(250)
                .HasColumnType("character varying(250)");

            b.Property<string>("VectorDescription")
                .HasColumnType("text");

            b.HasKey("Id");

            b.ToTable("Game");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.GameEnrichmentQueue", b =>
        {
            b.Property<Guid>("GameId")
                .HasColumnType("uuid");

            b.Property<DateTime>("AddedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("RetryCount")
                .HasColumnType("integer");

            b.Property<int>("Status")
                .HasColumnType("integer");

            b.HasKey("GameId");

            b.ToTable("GameEnrichmentQueue");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.GamePlayed", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<DateTime>("Date")
                .HasColumnType("timestamp with time zone");

            b.Property<Guid>("GameId")
                .HasColumnType("uuid");

            b.Property<string>("Language")
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<Guid>("StreamerId")
                .HasColumnType("uuid");

            b.Property<int>("ViewerCount")
                .HasColumnType("integer");

            b.HasKey("Id");

            b.HasIndex("Date");

            b.HasIndex("GameId");

            b.HasIndex("StreamerId", "GameId", "Date")
                .IsUnique();

            b.ToTable("GamePlayed");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.GameTwitch", b =>
        {
            b.Property<string>("TwitchId")
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<Guid>("GameId")
                .HasColumnType("uuid");

            b.Property<string>("TwitchName")
                .IsRequired()
                .HasMaxLength(250)
                .HasColumnType("character varying(250)");

            b.HasKey("TwitchId");

            b.HasIndex("GameId");

            b.ToTable("GameTwitch");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.Streamer", b =>
        {
            b.Property<Guid>("Id")
                .HasColumnType("uuid");

            b.Property<string>("Email")
                .HasMaxLength(320)
                .HasColumnType("character varying(320)");

            b.Property<DateTime>("LastModificationDate")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("PersonaDescription")
                .HasColumnType("text");

            b.Property<string>("VectorDescription")
                .HasColumnType("text");

            b.HasKey("Id");

            b.HasIndex("Email");

            b.ToTable("Streamer");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.StreamerEnrichmentQueue", b =>
        {
            b.Property<Guid>("StreamerId")
                .HasColumnType("uuid");

            b.Property<DateTime>("AddedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<int>("RetryCount")
                .HasColumnType("integer");

            b.Property<int>("Status")
                .HasColumnType("integer");

            b.HasKey("StreamerId");

            b.ToTable("StreamerEnrichmentQueue");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.StreamerTwitch", b =>
        {
            b.Property<string>("TwitchId")
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<Guid>("StreamerId")
                .HasColumnType("uuid");

            b.Property<string>("TwitchLogin")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.HasKey("TwitchId");

            b.HasIndex("StreamerId");

            b.ToTable("StreamerTwitch");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.StreamerYoutube", b =>
        {
            b.Property<Guid>("StreamerId")
                .HasColumnType("uuid");

            b.Property<string>("YoutubeUrl")
                .IsRequired()
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.HasKey("StreamerId");

            b.ToTable("StreamerYoutube");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.WorkerState", b =>
        {
            b.Property<string>("WorkerName")
                .HasMaxLength(200)
                .HasColumnType("character varying(200)");

            b.Property<string>("CurrentCursor")
                .HasColumnType("text");

            b.Property<DateTime>("LastCleanupDate")
                .HasColumnType("timestamp with time zone");

            b.HasKey("WorkerName");

            b.ToTable("WorkerState");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.GameEnrichmentQueue", b =>
        {
            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Game", "Game")
                .WithOne("EnrichmentQueue")
                .HasForeignKey("Symbolics.Com.Core.Infrastructure.Entities.GameEnrichmentQueue", "GameId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Game");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.GamePlayed", b =>
        {
            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Game", "Game")
                .WithMany("PlayedEntries")
                .HasForeignKey("GameId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Streamer", "Streamer")
                .WithMany("PlayedEntries")
                .HasForeignKey("StreamerId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Game");

            b.Navigation("Streamer");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.GameTwitch", b =>
        {
            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Game", "Game")
                .WithMany("TwitchMappings")
                .HasForeignKey("GameId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Game");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.StreamerEnrichmentQueue", b =>
        {
            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Streamer", "Streamer")
                .WithOne("EnrichmentQueue")
                .HasForeignKey("Symbolics.Com.Core.Infrastructure.Entities.StreamerEnrichmentQueue", "StreamerId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Streamer");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.StreamerTwitch", b =>
        {
            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Streamer", "Streamer")
                .WithMany("TwitchMappings")
                .HasForeignKey("StreamerId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Streamer");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.StreamerYoutube", b =>
        {
            b.HasOne("Symbolics.Com.Core.Infrastructure.Entities.Streamer", "Streamer")
                .WithOne("Youtube")
                .HasForeignKey("Symbolics.Com.Core.Infrastructure.Entities.StreamerYoutube", "StreamerId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Streamer");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.Game", b =>
        {
            b.Navigation("EnrichmentQueue");

            b.Navigation("PlayedEntries");

            b.Navigation("TwitchMappings");
        });

        modelBuilder.Entity("Symbolics.Com.Core.Infrastructure.Entities.Streamer", b =>
        {
            b.Navigation("EnrichmentQueue");

            b.Navigation("PlayedEntries");

            b.Navigation("TwitchMappings");

            b.Navigation("Youtube");
        });
    }
}

using Microsoft.EntityFrameworkCore;
using Symbolics.Com.Core.Infrastructure.Entities;

namespace Symbolics.Com.Core.Infrastructure.Persistence;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<WorkerState> WorkerStates => Set<WorkerState>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameTwitch> GameTwitches => Set<GameTwitch>();
    public DbSet<GameEnrichmentQueue> GameEnrichmentQueues => Set<GameEnrichmentQueue>();
    public DbSet<Streamer> Streamers => Set<Streamer>();
    public DbSet<StreamerTwitch> StreamerTwitches => Set<StreamerTwitch>();
    public DbSet<StreamerYoutube> StreamerYoutubes => Set<StreamerYoutube>();
    public DbSet<StreamerEnrichmentQueue> StreamerEnrichmentQueues => Set<StreamerEnrichmentQueue>();
    public DbSet<GamePlayed> GamePlays => Set<GamePlayed>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<ExternalServiceLog> ExternalServiceLogs => Set<ExternalServiceLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkerState>(entity =>
        {
            entity.ToTable("WorkerState");
            entity.HasKey(e => e.WorkerName);
            entity.Property(e => e.WorkerName).HasMaxLength(200);
            entity.Property(e => e.CurrentCursor).HasColumnType("text");
            entity.Property(e => e.LastCleanupDate);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.ToTable("Game");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(250).IsRequired();
            entity.Property(e => e.VectorDescription).HasColumnType("text");
        });

        modelBuilder.Entity<GameTwitch>(entity =>
        {
            entity.ToTable("GameTwitch");
            entity.HasKey(e => e.TwitchId);
            entity.Property(e => e.TwitchId).HasMaxLength(200);
            entity.Property(e => e.TwitchName).HasMaxLength(250).IsRequired();
            entity.HasOne(e => e.Game)
                .WithMany(e => e.TwitchMappings)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameEnrichmentQueue>(entity =>
        {
            entity.ToTable("GameEnrichmentQueue");
            entity.HasKey(e => e.GameId);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne(e => e.Game)
                .WithOne(e => e.EnrichmentQueue)
                .HasForeignKey<GameEnrichmentQueue>(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Streamer>(entity =>
        {
            entity.ToTable("Streamer");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VectorDescription).HasColumnType("text");
            entity.Property(e => e.PersonaDescription).HasColumnType("text");
            entity.Property(e => e.Email).HasMaxLength(320);
            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<StreamerTwitch>(entity =>
        {
            entity.ToTable("StreamerTwitch");
            entity.HasKey(e => e.TwitchId);
            entity.Property(e => e.TwitchId).HasMaxLength(200);
            entity.Property(e => e.TwitchLogin).HasMaxLength(200).IsRequired();
            entity.Property(e => e.TwitchName).HasMaxLength(250).IsRequired();
            entity.HasOne(e => e.Streamer)
                .WithMany(e => e.TwitchMappings)
                .HasForeignKey(e => e.StreamerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StreamerYoutube>(entity =>
        {
            entity.ToTable("StreamerYoutube");
            entity.HasKey(e => e.StreamerId);
            entity.Property(e => e.YoutubeUrl).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.Streamer)
                .WithOne(e => e.Youtube)
                .HasForeignKey<StreamerYoutube>(e => e.StreamerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StreamerEnrichmentQueue>(entity =>
        {
            entity.ToTable("StreamerEnrichmentQueue");
            entity.HasKey(e => e.StreamerId);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.HasOne(e => e.Streamer)
                .WithOne(e => e.EnrichmentQueue)
                .HasForeignKey<StreamerEnrichmentQueue>(e => e.StreamerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GamePlayed>(entity =>
        {
            entity.ToTable("GamePlayed");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Language).HasMaxLength(20).IsRequired();
            entity.Property(e => e.TwitchStreamId).HasMaxLength(200).IsRequired();
            entity.HasOne(e => e.Streamer)
                .WithMany(e => e.PlayedEntries)
                .HasForeignKey(e => e.StreamerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Game)
                .WithMany(e => e.PlayedEntries)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.StreamerId, e.GameId, e.Date }).IsUnique();
            entity.HasIndex(e => e.TwitchStreamId).IsUnique();
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("Campaign");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserDescription).HasColumnType("text");
            entity.Property(e => e.OptimizedDescription).HasColumnType("text");
        });

        modelBuilder.Entity<ExternalServiceLog>(entity =>
        {
            entity.ToTable("ExternalServiceLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Service).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ActionType).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(200);
            entity.Property(e => e.ExecutionTimeMs);
            entity.Property(e => e.CreatedAt);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Service, e.ActionType });
        });
    }
}

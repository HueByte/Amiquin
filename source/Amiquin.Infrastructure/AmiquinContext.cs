using Amiquin.Core;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Amiquin.Infrastructure;

public class AmiquinContext : DbContext
{
    public AmiquinContext()
    {

    }

    public AmiquinContext(DbContextOptions<AmiquinContext> options) : base(options)
    {

    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var environment = Environment.GetEnvironmentVariable(Constants.DefaultValues.ContainerEnvironmentVariable);
            if (environment == Constants.DefaultValues.ContainerEnvironmentValue)
            {
                // Use a default SQLite configuration for design-time
                optionsBuilder.UseSqlite(Constants.DefaultValues.InMemoryDatabase);
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Configure relationships
        builder.Entity<Message>()
            .HasOne(m => m.Server)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Toggle>()
            .HasOne(t => t.Server)
            .WithMany(s => s.Toggles)
            .HasForeignKey(t => t.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CommandLog>()
            .HasOne(c => c.Server)
            .WithMany(s => s.CommandLogs)
            .HasForeignKey(c => c.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<NachoPack>()
            .HasOne(n => n.Server)
            .WithMany(s => s.NachoPacks)
            .HasForeignKey(n => n.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Chat Session

        // Primary index for session lookups by scope and owning entity
        builder.Entity<ChatSession>()
            .HasIndex(cs => new { cs.Scope, cs.OwningEntityId })
            .HasDatabaseName(Constants.DatabaseIndexNames.ChatSessionsScopeOwner);

        // Additional indexes for performance
        builder.Entity<ChatSession>()
            .HasIndex(cs => new { cs.IsActive, cs.LastActivityAt })
            .HasDatabaseName(Constants.DatabaseIndexNames.ChatSessionsActivity);

        builder.Entity<ChatSession>()
            .HasIndex(cs => cs.CreatedAt)
            .HasDatabaseName(Constants.DatabaseIndexNames.ChatSessionsCreated);

        // Configure Scope enum to be stored as integer
        builder.Entity<ChatSession>()
            .Property(cs => cs.Scope)
            .HasConversion<int>();

        builder.Entity<SessionMessage>()
            .HasOne(sm => sm.ChatSession)
            .WithMany(cs => cs.Messages)
            .HasForeignKey(sm => sm.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SessionMessage>()
            .HasIndex(sm => sm.DiscordMessageId);

        builder.Entity<SessionMessage>()
            .HasIndex(sm => sm.CreatedAt);

        #region ServerMeta Configuration

        builder.Entity<ServerMeta>()
            .HasMany(s => s.Toggles)
            .WithOne(t => t.Server)
            .HasForeignKey(t => t.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServerMeta>()
            .HasMany(s => s.Messages)
            .WithOne(m => m.Server)
            .HasForeignKey(m => m.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServerMeta>()
            .HasMany(s => s.CommandLogs)
            .WithOne(c => c.Server)
            .HasForeignKey(c => c.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ServerMeta>()
            .HasMany(s => s.NachoPacks)
            .WithOne(n => n.Server)
            .HasForeignKey(n => n.ServerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ServerMeta>()
            .HasKey(s => s.Id);

        #endregion

        base.OnModelCreating(builder);
    }

    public DbSet<ServerMeta> ServerMetas { get; set; } = default!;
    public DbSet<Message> Messages { get; set; } = default!;
    public DbSet<Toggle> Toggles { get; set; } = default!;
    public DbSet<CommandLog> CommandLogs { get; set; } = default!;
    public DbSet<NachoPack> NachoPacks { get; set; } = default!;
    public DbSet<BotStatistics> BotStatistics { get; set; } = default!;
    public DbSet<ChatSession> ChatSessions { get; set; } = default!;
    public DbSet<SessionMessage> SessionMessages { get; set; } = default!;
}
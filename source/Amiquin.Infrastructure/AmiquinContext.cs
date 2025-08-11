using Amiquin.Core;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using InfraUserStats = Amiquin.Infrastructure.Entities.UserStats;

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
            var aspNetEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (environment == Constants.DefaultValues.ContainerEnvironmentValue || aspNetEnvironment == "Development")
            {
                // Use a default SQLite configuration for design-time and development
                optionsBuilder.UseSqlite(Constants.DefaultValues.InMemoryDatabase);
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();

                // Suppress the pending model changes warning in design-time scenarios
                optionsBuilder.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
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
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<NachoPack>()
            .HasOne(n => n.Server)
            .WithMany(s => s.NachoPacks)
            .HasForeignKey(n => n.ServerId)
            .OnDelete(DeleteBehavior.SetNull);

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

        // Configure Pagination Sessions
        builder.Entity<PaginationSession>()
            .HasIndex(ps => new { ps.UserId, ps.IsActive })
            .HasDatabaseName("IX_PaginationSessions_UserActive");

        builder.Entity<PaginationSession>()
            .HasIndex(ps => ps.MessageId);

        builder.Entity<PaginationSession>()
            .HasIndex(ps => ps.ExpiresAt);

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
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ServerMeta>()
            .HasMany(s => s.NachoPacks)
            .WithOne(n => n.Server)
            .HasForeignKey(n => n.ServerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ServerMeta>()
            .HasKey(s => s.Id);

        #endregion
        
        // Configure UserStats unique constraint  
        builder.Entity<InfraUserStats>()
            .HasIndex(u => new { u.UserId, u.ServerId })
            .IsUnique();

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
    public DbSet<PaginationSession> PaginationSessions { get; set; } = default!;
    public DbSet<InfraUserStats> UserStats { get; set; } = default!;
}
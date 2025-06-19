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

        base.OnModelCreating(builder);
    }

    public DbSet<ServerMeta> ServerMetas { get; set; } = default!;
    public DbSet<Message> Messages { get; set; } = default!;
    public DbSet<Toggle> Toggles { get; set; } = default!;
    public DbSet<CommandLog> CommandLogs { get; set; } = default!;
    public DbSet<NachoPack> NachoPacks { get; set; } = default!;
    public DbSet<BotStatistics> BotStatistics { get; set; } = default!;
}
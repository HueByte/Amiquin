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
        base.OnModelCreating(builder);
    }

    public DbSet<Message> Messages { get; set; } = default!;
    public DbSet<Toggle> Toggles { get; set; } = default!;
}
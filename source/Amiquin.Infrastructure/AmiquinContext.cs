using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Amiquin.Infrastructure;

public class AmiquinContext : DbContext
{
    public AmiquinContext(DbContextOptions<AmiquinContext> options) : base(options)
    {

    }

    public DbSet<Message> Messages { get; set; } = default!;
}
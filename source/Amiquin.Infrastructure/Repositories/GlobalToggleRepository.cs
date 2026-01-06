using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for global toggle operations.
/// </summary>
public class GlobalToggleRepository : QueryableBaseRepository<string, GlobalToggle, AmiquinContext>, IGlobalToggleRepository
{
    public GlobalToggleRepository(AmiquinContext context) : base(context)
    {
    }
}

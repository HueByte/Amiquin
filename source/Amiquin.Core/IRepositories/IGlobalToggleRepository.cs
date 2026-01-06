using Amiquin.Core.Abstraction;
using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

/// <summary>
/// Repository interface for global toggle operations.
/// </summary>
public interface IGlobalToggleRepository : IQueryableRepository<string, GlobalToggle>
{
}

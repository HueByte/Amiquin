using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Cleaner;

/// <summary>
/// Service interface for cleaning and maintenance operations.
/// Extends IRunnableJob to provide scheduled cleanup functionality.
/// </summary>
public interface ICleanerService : IRunnableJob
{
}

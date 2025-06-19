using Amiquin.Core.Abstraction;

namespace Amiquin.Core.IRepositories;

public interface ICommandLogRepository : IQueryableRepository<int, Models.CommandLog>
{

}
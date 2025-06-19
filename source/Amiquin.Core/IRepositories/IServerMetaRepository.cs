using Amiquin.Core.Abstraction;
using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

public interface IServerMetaRepository : IQueryableRepository<ulong, ServerMeta>
{

}
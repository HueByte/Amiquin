using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

public class ServerMetaRepository : QueryableBaseRepository<ulong, ServerMeta, AmiquinContext>, IServerMetaRepository
{
    public ServerMetaRepository(AmiquinContext context) : base(context) { }
}
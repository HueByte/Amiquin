using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

public class NachoRepository : QueryableBaseRepository<int, NachoPack, AmiquinContext>, INachoRepository
{
    public NachoRepository(AmiquinContext context) : base(context) { }
}
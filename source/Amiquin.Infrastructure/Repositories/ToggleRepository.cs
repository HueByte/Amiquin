using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

public class ToggleRepository : QueryableBaseRepository<string, Toggle, AmiquinContext>, IToggleRepository
{
    public ToggleRepository(AmiquinContext context) : base(context)
    {

    }
}
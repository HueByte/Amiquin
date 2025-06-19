using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

public class CommandLogRepository : QueryableBaseRepository<int, CommandLog, AmiquinContext>, ICommandLogRepository
{
    public CommandLogRepository(AmiquinContext context) : base(context) { }
}
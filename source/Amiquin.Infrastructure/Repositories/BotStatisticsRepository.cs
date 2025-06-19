using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

public class BotStatisticsRepository : QueryableBaseRepository<string, BotStatistics, AmiquinContext>, IBotStatisticsRepository
{
    public BotStatisticsRepository(AmiquinContext context) : base(context) { }
}
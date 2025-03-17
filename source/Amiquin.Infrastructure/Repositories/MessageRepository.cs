using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;

namespace Amiquin.Infrastructure.Repositories;

public class MessageRepository : QueryableBaseRepository<string, Message, AmiquinContext>, IMessageRepository
{
    public MessageRepository(AmiquinContext context) : base(context)
    {

    }
}
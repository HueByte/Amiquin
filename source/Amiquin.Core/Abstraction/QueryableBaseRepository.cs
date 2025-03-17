using Microsoft.EntityFrameworkCore;

namespace Amiquin.Core.Abstraction;

public class QueryableBaseRepository<TKeyType, TEntity, TContext> : BaseRepository<TKeyType, TEntity, TContext>, IQueryableRepository<TKeyType, TEntity>
    where TKeyType : IConvertible
    where TEntity : DbModel<TKeyType>, new()
    where TContext : DbContext, new()
{
    public QueryableBaseRepository(TContext context) : base(context) { }
    public override IQueryable<TEntity> AsQueryable()
    {
        return _context.Set<TEntity>().AsQueryable();
    }
}

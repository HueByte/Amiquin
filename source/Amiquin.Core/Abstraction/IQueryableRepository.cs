namespace Amiquin.Core.Abstraction;

public interface IQueryableRepository<TKey, TEntity> : IRepository<TKey, TEntity>
    where TKey : IConvertible
    where TEntity : DbModel<TKey>
{
    IQueryable<TEntity> AsQueryable();
}

using System.ComponentModel.DataAnnotations.Schema;

namespace Amiquin.Core.Abstraction;

public abstract class DbModel<TKey> where TKey : IConvertible
{
    public virtual TKey Id { get; set; } = default!;
}

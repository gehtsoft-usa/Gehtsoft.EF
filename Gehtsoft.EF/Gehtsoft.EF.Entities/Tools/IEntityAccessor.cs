using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    public interface IEntityAccessor<T> : ICollection<T>
    {
        T this[int index] { get; }

        int Find(T entity, IEqualityComparer<T> comparer);

        int Find(T entity);

        bool Contains(T entity, IEqualityComparer<T> comparer);

        T[] ToArray();
    }
}
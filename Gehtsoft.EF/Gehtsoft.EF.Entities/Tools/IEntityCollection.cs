using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    public interface IEntityCollection<T> : IEntityAccessor<T>, IList<T>
    {
        new T this[int index] { get; set; }
    }
}
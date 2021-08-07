using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    public class EntityEqualityComparer<T> : IEqualityComparer<T>
    {
        public bool Equals(T x, T y)
        {
            return EntityComparerHelper.Equals(x, y);
        }

        public int GetHashCode(T obj) => EntityComparerHelper.GetHashCode(obj);
    }
}

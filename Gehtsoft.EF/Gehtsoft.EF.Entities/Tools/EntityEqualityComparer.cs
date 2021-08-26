using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The implementation of the equality comparer for entities.
    ///
    /// The implementation is based on <see cref="EntityComparerHelper"/> class.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EntityEqualityComparer<T> : IEqualityComparer<T>
    {
        /// <summary>Determines whether the specified objects are equal.</summary>
        /// <param name="x">The first object of type T to compare.</param>
        /// <param name="y">The second object of type T to compare.</param>
        /// <returns>true if the specified objects are equal; otherwise, false.</returns>
        public bool Equals(T x, T y)
        {
            return EntityComparerHelper.Equals(x, y);
        }

        /// <summary>Returns a hash code for the specified object.</summary>
        /// <param name="obj">The <see cref="object"></see> for which a hash code is to be returned.</param>
        /// <returns>A hash code for the specified object.</returns>
        /// <exception cref="System.ArgumentNullException">The type of <paramref name="obj">obj</paramref> is a reference type and <paramref name="obj">obj</paramref> is null.</exception>
        public int GetHashCode(T obj) => EntityComparerHelper.GetHashCode(obj);
    }
}

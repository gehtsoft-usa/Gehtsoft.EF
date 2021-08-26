using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The interface to the collection of the entities.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEntityAccessor<T> : ICollection<T>
    {
        /// <summary>
        /// Gets the entity by its index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        T this[int index] { get; }

        /// <summary>
        /// Finds the entity using the specified comparer.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        int Find(T entity, IEqualityComparer<T> comparer);

        /// <summary>
        /// Finds the entity using the default comparer.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        int Find(T entity);

        /// <summary>
        /// Checks whether the entity contains the element equal to the value specified.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="comparer"></param>
        /// <returns></returns>
        bool Contains(T entity, IEqualityComparer<T> comparer);

        /// <summary>
        /// Converts the collection to an array.
        /// </summary>
        /// <returns></returns>
        T[] ToArray();
    }
}
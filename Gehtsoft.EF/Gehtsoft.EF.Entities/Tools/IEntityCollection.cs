using System.Collections.Generic;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The interface to the collection of the entities
    ///
    /// Use <see cref="EntityCollection{T}"/> as the default implementation of this interface.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEntityCollection<T> : IEntityAccessor<T>, IList<T>
    {
        /// <summary>
        /// Gets or sets the value by its index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        new T this[int index] { get; set; }
    }
}
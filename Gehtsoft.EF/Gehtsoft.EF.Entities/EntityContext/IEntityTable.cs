using System;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The storage object associated with the entity type
    /// </summary>
    public interface IEntityTable
    {
        /// <summary>
        /// The name of the object
        /// </summary>
        string Name { get; }
        /// <summary>
        /// The type of the entity.
        /// </summary>
        Type EntityType { get; }
    }
}
using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The default implementation of the dynamic entity property information interface.
    ///
    /// The interface is used to return entity information from the <see cref="DynamicEntity"/>.
    ///
    /// Use <see cref="DynamicEntityProperty"/> as a dynamic implementation.
    /// </summary>
    public interface IDynamicEntityProperty
    {
        /// <summary>
        /// The type of the property
        /// </summary>
        Type PropertyType { get; }
        /// <summary>
        /// The name of the property
        /// </summary>
        string Name { get; }
        /// <summary>
        /// The entity property attribute
        /// </summary>
        EntityPropertyAttribute EntityPropertyAttribute { get; }
        /// <summary>
        /// Obsolte entity property attribute.
        /// </summary>
        ObsoleteEntityPropertyAttribute ObsoleteEntityPropertyAttribute { get; }
    }
}
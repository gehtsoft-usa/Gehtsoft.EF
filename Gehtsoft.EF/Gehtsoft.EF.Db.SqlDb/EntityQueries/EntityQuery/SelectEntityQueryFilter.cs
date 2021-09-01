using System;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The filter to exclude properties from a select query.
    /// </summary>
    public class SelectEntityQueryFilter
    {
        /// <summary>
        /// The entity to exclude
        /// </summary>
        public Type EntityType { get; set; }
        /// <summary>
        /// The property name to exclude.
        /// </summary>
        public string Property { get; set; }
    }
}
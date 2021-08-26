using System;
using System.Data;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The attribute to mark an entity property as an integer primary key with auto increment.
    ///
    /// You can use this attribute instead of <see cref="EntityPropertyAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class AutoIdAttribute : EntityPropertyAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AutoIdAttribute()
        {
            DbType = DbType.Int32;
            AutoId = true;
        }
    }
}

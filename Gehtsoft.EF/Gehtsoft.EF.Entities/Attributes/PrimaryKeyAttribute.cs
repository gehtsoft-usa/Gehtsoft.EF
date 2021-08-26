using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The attribute to mark a property as a primary key.
    ///
    /// You can use it instead of <see cref="EntityPropertyAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class PrimaryKeyAttribute : EntityPropertyAttribute
    {
        public PrimaryKeyAttribute()
        {
            PrimaryKey = true;
        }
    }
}

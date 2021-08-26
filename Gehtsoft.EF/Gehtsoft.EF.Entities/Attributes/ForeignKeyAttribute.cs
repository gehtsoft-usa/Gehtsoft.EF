using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The flag indicating that the entity property is a foreign key.
    ///
    /// You can use this attribute instead of <see cref="EntityPropertyAttribute"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class ForeignKeyAttribute : EntityPropertyAttribute
    {
        public ForeignKeyAttribute()
        {
            ForeignKey = true;
        }
    }
}

using System;

namespace Gehtsoft.EF.Entities
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class ForeignKeyAttribute : EntityPropertyAttribute
    {
        public ForeignKeyAttribute()
        {
            ForeignKey = true;
        }
    }
}

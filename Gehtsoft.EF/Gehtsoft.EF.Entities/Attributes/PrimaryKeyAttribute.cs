using System;

namespace Gehtsoft.EF.Entities
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class PrimaryKeyAttribute : EntityPropertyAttribute
    {
        public PrimaryKeyAttribute()
        {
            PrimaryKey = true;
        }
    }
}

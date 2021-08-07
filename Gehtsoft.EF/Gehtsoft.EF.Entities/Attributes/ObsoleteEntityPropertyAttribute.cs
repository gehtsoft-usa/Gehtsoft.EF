using System;

namespace Gehtsoft.EF.Entities
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class ObsoleteEntityPropertyAttribute : Attribute
    {
        public string Field { get; set; }
        public bool ForeignKey { get; set; }
        public bool Sorted { get; set; }

        public ObsoleteEntityPropertyAttribute()
        {
        }
    }
}

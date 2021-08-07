using System;
using System.Data;

namespace Gehtsoft.EF.Entities
{
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class EntityPropertyAttribute : Attribute
    {
        public string Field { get; set; }
        public DbType DbType { get; set; } = DbType.Object;
        public int Size { get; set; } = 0;
        public int Precision { get; set; } = 0;
        public bool AutoId { get; set; }
        public bool PrimaryKey { get; set; }
        public bool Autoincrement { get; set; }
        public bool ForeignKey { get; set; }
        public bool Sorted { get; set; }
        public bool Unique { get; set; }
        public bool Nullable { get; set; }
        public bool View { get; set; }
        public string Alias { get; set; }
        public bool IgnoreRead { get; set; }
        public object DefaultValue { get; set; }
        public bool IgnoreSerialization { get; set; }

        public EntityPropertyAttribute()
        {
            View = false;
            Nullable = false;
        }
    }
}

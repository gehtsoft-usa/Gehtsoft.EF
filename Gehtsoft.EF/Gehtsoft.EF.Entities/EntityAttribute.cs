using System;
using System.Data;
using System.Reflection;

namespace Gehtsoft.EF.Entities
{
    public enum EntityNamingPolicy
    {
        Default,
        BackwardCompatibility,
        AsIs,
        LowerCase,
        UpperCase,
        LowerFirstCharacter,
        UpperFirstCharacter,
        LowerCaseWithUnderscores,
        UpperCaseWithUnderscopes,
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class EntityAttribute : Attribute
    {
        public string Scope { get; set; }

        public string Table { get; set; }

        public bool View { get; set; }

        public Type Metadata { get; set; }

        public EntityNamingPolicy NamingPolicy { get; set; } = EntityNamingPolicy.Default;

        public EntityAttribute() : base()
        {
            Scope = null;
            View = false;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class ObsoleteEntityAttribute : Attribute
    {
        public string Scope { get; set; }

        public string Table { get; set; }

        public EntityNamingPolicy NamingPolicy { get; set; } = EntityNamingPolicy.Default;

        public bool View { get; set; }

        public Type Metadata { get; set; }

        public ObsoleteEntityAttribute() : base()
        {
            Scope = null;
        }
    }

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

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class AutoIdAttribute : EntityPropertyAttribute
    {
        public AutoIdAttribute()
        {
            DbType = DbType.Int32;
            AutoId = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class PrimaryKeyAttribute : EntityPropertyAttribute
    {
        public PrimaryKeyAttribute()
        {
            PrimaryKey = true;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class ForeignKeyAttribute : EntityPropertyAttribute
    {
        public ForeignKeyAttribute()
        {
            ForeignKey = true;
        }
    }

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

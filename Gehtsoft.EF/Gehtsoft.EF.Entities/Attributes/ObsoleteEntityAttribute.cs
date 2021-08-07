using System;

namespace Gehtsoft.EF.Entities
{
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
}

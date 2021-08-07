using System;
using System.Reflection;

namespace Gehtsoft.EF.Entities
{

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
}

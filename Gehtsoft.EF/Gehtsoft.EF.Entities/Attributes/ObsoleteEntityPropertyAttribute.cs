using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The attribute to mark obsolete properties
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class ObsoleteEntityPropertyAttribute : Attribute
    {
        /// <summary>
        /// The associated field name
        /// </summary>
        public string Field { get; set; }
        /// <summary>
        /// The flag indicating that the field is a foreign key.
        /// </summary>
        public bool ForeignKey { get; set; }
        /// <summary>
        /// The flag indicating that the field has an associated index.
        /// </summary>
        public bool Sorted { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public ObsoleteEntityPropertyAttribute()
        {
        }
    }
}

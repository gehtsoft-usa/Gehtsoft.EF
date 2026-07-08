using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Marks an entity property whose value is stored as a JSON document in a single database column.
    ///
    /// The property value (a primitive, an array of primitives, or a `System.Text.Json`-annotated
    /// object) is serialized to a JSON string on save and deserialized on load. Individual values
    /// inside the document can be filtered, sorted and indexed by their JSON path (see
    /// <see cref="JsonIndexAttribute"/>).
    ///
    /// Use this attribute instead of <see cref="EntityPropertyAttribute"/> on the property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class JsonEntityPropertyAttribute : Attribute
    {
        /// <summary>
        /// The name of the column.
        ///
        /// If the value is not set, the column name is created using the entity naming policy
        /// (<see cref="EntityAttribute.NamingPolicy"/>).
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The flag indicating that the column can hold a `NULL` value.
        ///
        /// A `null` CLR value is stored as SQL `NULL` (not as the JSON text `"null"`).
        /// </summary>
        public bool Nullable { get; set; }
    }
}

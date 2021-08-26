using System;
using System.Data;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The attribute to mark an entity property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class EntityPropertyAttribute : Attribute
    {
        /// <summary>
        /// The name of the field.
        ///
        /// If the value is not set, the name of the field
        /// will be created using the appropriate
        /// naming policy (<see cref="EntityAttribute.NamingPolicy"/>).
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The database field type.
        ///
        /// If the value is not set, the database type
        /// will be calculate automatically using
        /// the rules for each database.
        /// </summary>
        public DbType DbType { get; set; } = DbType.Object;

        /// <summary>
        /// The maximum size of the column.
        ///
        /// If is important to set the size to string, binary
        /// and numeric data types to avoid bloating the tables.
        /// </summary>
        public int Size { get; set; } = 0;
        /// <summary>
        /// The number of the positions after decimal point
        /// for real numbers.
        /// </summary>
        public int Precision { get; set; } = 0;

        /// <summary>
        /// The flag indicating that the property is
        /// a primary key with auto increment.
        /// </summary>
        public bool AutoId { get; set; }
        /// <summary>
        /// The flag indicating that the property
        /// is a primary key.
        /// </summary>
        public bool PrimaryKey { get; set; }
        /// <summary>
        /// The flag indicating that property
        /// is auto increment value.
        /// </summary>
        public bool Autoincrement { get; set; }
        /// <summary>
        /// The value indicating that the property is
        /// a reference to another entity.
        /// </summary>
        public bool ForeignKey { get; set; }
        /// <summary>
        /// The flag forcing creating an index for the column.
        /// </summary>
        public bool Sorted { get; set; }
        /// <summary>
        /// The flag indicating that the value is unique.
        /// </summary>
        public bool Unique { get; set; }
        /// <summary>
        /// The flag indicating that the value can be a `null` value.
        ///
        /// NOTE: If type is automatically defined and the property value
        /// has a nullable type, the column will be created with a nullable value.
        /// </summary>
        public bool Nullable { get; set; }

        /// <summary>
        /// The flag to prevent automatic including the entity
        /// property to "real all" queries.
        /// </summary>
        public bool IgnoreRead { get; set; }

        /// <summary>
        /// The default value.
        ///
        /// Only primitive types, such as string or numbers are supported.
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public EntityPropertyAttribute()
        {
            Nullable = false;
        }
    }
}

using System;
using System.Data;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Declares an index over a single primitive value located at a JSON path inside a
    /// <see cref="JsonEntityPropertyAttribute">JSON property</see>.
    ///
    /// The attribute is repeatable: apply it once per indexed path. The path may reach a primitive
    /// nested inside an object (for example <c>"$.address.zip"</c>). Only primitive value types are
    /// supported (string, integer types, real/decimal, boolean and date/time); arrays,
    /// <c>byte[]</c> and whole nested objects cannot be indexed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public class JsonIndexAttribute : Attribute
    {
        /// <summary>
        /// The JSON path to the indexed value, for example <c>"$.age"</c> or <c>"$.address.zip"</c>.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The primitive type of the value at the path. It drives the extraction/cast used when the
        /// index is created and when the value is queried. Defaults to <see cref="DbType.String"/>.
        /// </summary>
        public DbType DbType { get; set; } = DbType.String;

        /// <summary>
        /// The flag indicating that the indexed value is unique.
        /// </summary>
        public bool Unique { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="path">The JSON path to the indexed value.</param>
        public JsonIndexAttribute(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="path">The JSON path to the indexed value.</param>
        /// <param name="dbType">The primitive type of the value at the path.</param>
        public JsonIndexAttribute(string path, DbType dbType)
        {
            Path = path;
            DbType = dbType;
        }
    }
}

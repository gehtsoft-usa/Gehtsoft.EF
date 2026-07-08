using System;
using System.Collections.Generic;
using System.Data;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    /// <summary>
    /// Describes a JSON column: the CLR type serialized into it and the value indexes declared on it.
    ///
    /// It is attached to <see cref="QueryBuilder.TableDescriptor.ColumnInfo.Json"/> when the entity
    /// property is marked with <see cref="Gehtsoft.EF.Entities.JsonEntityPropertyAttribute"/>.
    /// </summary>
    public sealed class JsonColumnMetadata
    {
        /// <summary>
        /// The CLR type stored in the column (serialized to / deserialized from JSON).
        /// </summary>
        public Type ClrType { get; }

        /// <summary>
        /// The indexes declared over individual values inside the JSON document.
        /// </summary>
        public IReadOnlyList<JsonIndexDefinition> Indexes { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonColumnMetadata"/> class.
        /// </summary>
        /// <param name="clrType">The CLR type serialized into the column.</param>
        /// <param name="indexes">The declared value indexes (may be empty).</param>
        public JsonColumnMetadata(Type clrType, IReadOnlyList<JsonIndexDefinition> indexes)
        {
            ClrType = clrType;
            Indexes = indexes ?? Array.Empty<JsonIndexDefinition>();
        }
    }

    /// <summary>
    /// Describes one index over a single primitive value at a JSON path inside a JSON column.
    /// </summary>
    public sealed class JsonIndexDefinition
    {
        /// <summary>
        /// The JSON path to the indexed value, for example <c>"$.age"</c>.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The primitive type of the value at the path.
        /// </summary>
        public DbType DbType { get; }

        /// <summary>
        /// Whether the index is unique.
        /// </summary>
        public bool Unique { get; }

        /// <summary>
        /// The logical index name. It is always derived automatically from the column, path and
        /// type (never user-supplied), so that changing the path or the type changes the name and
        /// the change is detected by schema update. The physical index name is
        /// <c>&lt;table&gt;_&lt;Name&gt;</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonIndexDefinition"/> class.
        /// </summary>
        /// <param name="path">The JSON path to the indexed value.</param>
        /// <param name="dbType">The primitive type of the value at the path.</param>
        /// <param name="unique">Whether the index is unique.</param>
        /// <param name="name">The logical index name.</param>
        public JsonIndexDefinition(string path, DbType dbType, bool unique, string name)
        {
            Path = path;
            DbType = dbType;
            Unique = unique;
            Name = name;
        }
    }
}

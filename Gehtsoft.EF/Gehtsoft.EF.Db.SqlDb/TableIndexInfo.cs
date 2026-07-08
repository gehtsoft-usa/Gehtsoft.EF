using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// Describes one physical index found on a table.
    ///
    /// Returned by <see cref="SqlDbConnection.GetTableIndexes(string)"/>. Every index on the table
    /// is reported; primary-key and unique-constraint backing indexes are included but flagged via
    /// <see cref="IsPrimary"/> / <see cref="IsUnique"/> so callers can identify and ignore them.
    /// </summary>
    public sealed class TableIndexInfo
    {
        /// <summary>
        /// The physical (database) name of the index, e.g. <c>"mytable_mycolumn"</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The ordered list of indexed column names (lower-cased). Empty when the index is over an
        /// expression rather than plain columns (see <see cref="IsExpression"/>).
        /// </summary>
        public IReadOnlyList<string> Columns { get; }

        /// <summary>
        /// `true` when at least one key part of the index is an expression rather than a plain
        /// column; in that case <see cref="Columns"/> cannot be relied upon for a structural
        /// comparison.
        /// </summary>
        public bool IsExpression { get; }

        /// <summary>
        /// `true` when the index enforces uniqueness (including the index backing a `UNIQUE`
        /// constraint).
        /// </summary>
        public bool IsUnique { get; }

        /// <summary>
        /// `true` when the index backs the table's `PRIMARY KEY`.
        /// </summary>
        public bool IsPrimary { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableIndexInfo"/> class.
        /// </summary>
        /// <param name="name">The physical index name.</param>
        /// <param name="columns">The ordered indexed column names (lower-cased); may be empty.</param>
        /// <param name="isExpression">Whether any key part is an expression.</param>
        /// <param name="isUnique">Whether the index is unique.</param>
        /// <param name="isPrimary">Whether the index backs the primary key.</param>
        public TableIndexInfo(string name, IReadOnlyList<string> columns, bool isExpression, bool isUnique, bool isPrimary)
        {
            Name = name;
            Columns = columns ?? System.Array.Empty<string>();
            IsExpression = isExpression;
            IsUnique = isUnique;
            IsPrimary = isPrimary;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The interface that sets or gets a property of the entity.
    /// </summary>
    public interface IPropertyAccessor
    {
        /// <summary>
        /// The name of the property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The type of the property.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets a value.
        /// </summary>
        /// <param name="thisObject"></param>
        /// <returns></returns>
        object GetValue(object thisObject);

        /// <summary>
        /// Sets a value.
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="value"></param>
        void SetValue(object thisObject, object value);

        /// <summary>
        /// Returns a custom attribute of the type specified.
        /// </summary>
        System.Attribute GetCustomAttribute(Type attributeType);
    }

    [DocgenIgnore]
    public static class PropertyAccessorExtension
    {
        public static T GetCustomAttribute<T>(this IPropertyAccessor accessor)
            where T : System.Attribute => (T)accessor.GetCustomAttribute(typeof(T));
    }

    [DocgenIgnore]
    public class PropertyAccessor : IPropertyAccessor
    {
        private readonly PropertyInfo mPropertyInfo;

        public PropertyAccessor(PropertyInfo propertyInfo)
        {
            mPropertyInfo = propertyInfo;
        }

        public string Name => mPropertyInfo.Name;
        public Type PropertyType => mPropertyInfo.PropertyType;
        public object GetValue(object thisObject) => mPropertyInfo.GetValue(thisObject);
        public void SetValue(object thisObject, object value) => mPropertyInfo.SetValue(thisObject, value);
        public Attribute GetCustomAttribute(Type attributeType) => mPropertyInfo.GetCustomAttribute(attributeType);
    }

    /// <summary>
    /// The definition of a table
    /// </summary>
    public sealed class TableDescriptor : IEnumerable<TableDescriptor.ColumnInfo>, IEquatable<TableDescriptor>
    {
        /// <summary>
        /// One column in a table definition
        /// </summary>
        public sealed class ColumnInfo : IEquatable<ColumnInfo>
        {
            /// <summary>
            /// The column identifier.
            ///
            /// When entity queries aren't used, the identifier is equal to the field name.
            ///
            /// When entity queries are used, the identifier is the name of the entity property.
            /// </summary>
            public string ID { get; set; }

            /// <summary>
            /// The SQL column name.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// SQL column type.
            /// </summary>
            public DbType DbType { get; set; }

            /// <summary>
            /// SQL column size.
            /// </summary>
            public int Size { get; set; }

            /// <summary>
            /// For numeric types, the number of digits after decimal point.
            /// </summary>
            public int Precision { get; set; }

            /// <summary>
            /// The flag indicating whether the column is a primary key.
            /// </summary>
            public bool PrimaryKey { get; set; }

            /// <summary>
            /// The flag indicating whether the column is an auto-increment value.
            /// </summary>
            public bool Autoincrement { get; set; }

            /// <summary>
            /// The flag indicating whether the column is sorted.
            /// </summary>
            public bool Sorted { get; set; }

            /// <summary>
            /// The flag indicating whether the column is unique.
            /// </summary>
            public bool Unique { get; set; }

            /// <summary>
            /// The flag indicating whether the column accepts `NULL` values.
            /// </summary>
            public bool Nullable { get; set; }

            /// <summary>
            /// The flag indicating whether the column refers to another table.
            /// </summary>
            public bool ForeignKey => ForeignTable != null;

            /// <summary>
            /// The table to which this column is referred.
            /// </summary>
            public TableDescriptor ForeignTable { get; set; }

            /// <summary>
            /// The table to which this column belongs.
            /// </summary>
            public TableDescriptor Table { get; internal set; }

            /// <summary>
            /// The property accessor to get or set property value.
            /// </summary>
            public IPropertyAccessor PropertyAccessor { get; internal set; }

            /// <summary>
            /// The flag indicating whether the column must be ignored
            /// when all entity properties are requested to be read.
            /// </summary>
            public bool IgnoreRead { get; set; }

            /// <summary>
            /// Default column value.
            /// </summary>
            public object DefaultValue { get; set; }

            private string mFullName = null;

            /// <summary>
            /// Full name of the column.
            ///
            /// Full name is the table name plus column name.
            /// </summary>
            public string FullName
            {
                get
                {
                    return mFullName ?? (mFullName = $"{Table.Name}.{Name}");
                }
            }

            [DocgenIgnore]
            public bool Equals(ColumnInfo other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(FullName, other.FullName);
            }

            [DocgenIgnore]
            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ColumnInfo)obj);
            }

            [DocgenIgnore]
            public override int GetHashCode()
            {
                unchecked
                {
                    return FullName.GetHashCode();
                }
            }

            [DocgenIgnore]
            public static bool operator ==(ColumnInfo obj1, ColumnInfo obj2)
            {
                if (obj1 is null || obj2 is null)
                    return Object.Equals(obj1, obj2);
                else
                    return obj1.Equals(obj2);
            }

            [DocgenIgnore]
            public static bool operator !=(ColumnInfo obj1, ColumnInfo obj2)
            {
                if (obj1 is null || obj2 is null)
                    return !Object.Equals(obj1, obj2);
                else
                    return !obj1.Equals(obj2);
            }
        }

        private readonly List<ColumnInfo> mColumns = new List<ColumnInfo>();

        /// <summary>
        /// The name of the table.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The scope of the table.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>
        /// The primary key column.
        /// </summary>
        public ColumnInfo PrimaryKey { get; private set; } = null;

        /// <summary>
        /// The flag indicating that the table is a view.
        /// </summary>
        public bool View { get; set; }

        /// <summary>
        /// The optional metadata object.
        ///
        /// The metadata object may be used to provide additional information, for example:
        /// * The metadata implements [clink=Gehtsoft.EF.Db.SqlDb.Metadata.ICompositeIndexMetadata]ICompositeIndexMetadata[/clink]
        ///   to provide information about composite indexes.
        /// * The metadata implements [clink=Gehtsoft.EF.Db.SqlDb.Metadata.IViewCreationMetadata]IViewCreationMetadata[/clink]
        ///   to provide select query for automatic view creation.
        /// </summary>
        public object Metadata { get; set; }

        /// <summary>
        /// The number of columns.
        /// </summary>
        public int Count => mColumns.Count;

        /// <summary>
        /// Gets a column by its index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public ColumnInfo this[int index] => mColumns[index];

        private class ColumnIndexComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                if (x == null && y == null)
                    return true;
                if (x == null || y == null)
                    return false;
                return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(string obj)
            {
                if (obj == null)
                    return 0;
                return obj.GetHashCode();
            }
        }

        private readonly Dictionary<string, ColumnInfo> mColumnsIndex = new Dictionary<string, ColumnInfo>(new ColumnIndexComparer());

        /// <summary>
        /// Gets the column by its id.
        ///
        /// See also <see cref="ColumnInfo.ID"/>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ColumnInfo this[string id] => mColumnsIndex[id];

        /// <summary>
        /// Checks whether the table has the column with the ID specified.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool HasColumn(string id) => mColumnsIndex.ContainsKey(id);

        /// <summary>
        /// Constructor
        /// </summary>
        public TableDescriptor()
        {
        }

        /// <summary>
        /// Constructor for the specified table name.
        /// </summary>
        /// <param name="name"></param>
        public TableDescriptor(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Constructor for the specified table name and column list.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="columns"></param>
        public TableDescriptor(string name, IEnumerable<ColumnInfo> columns)
        {
            Name = name;
            Add(columns);
        }

        /// <summary>
        /// Adds a column to the table descriptor.
        /// </summary>
        /// <param name="column"></param>
        public void Add(ColumnInfo column)
        {
            if (column == null)
                throw new ArgumentNullException(nameof(column));
            column.Table = this;
            if (column.PrimaryKey)
                PrimaryKey = column;
            if (column.ID == null)
                column.ID = column.Name;
            mColumns.Add(column);
            mColumnsIndex[column.ID] = column;
        }

        /// <summary>
        /// Adds columns to the table description.
        /// </summary>
        /// <param name="columns"></param>
        public void Add(IEnumerable<ColumnInfo> columns)
        {
            if (columns == null)
                throw new ArgumentNullException(nameof(columns));
            foreach (ColumnInfo column in columns)
                Add(column);
        }

        [DocgenIgnore]
        public IEnumerator<ColumnInfo> GetEnumerator()
        {
            return mColumns.GetEnumerator();
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mColumns).GetEnumerator();
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public bool Equals(TableDescriptor other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TableDescriptor)obj);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override int GetHashCode()
        {
            unchecked
            {
                return (Name?.GetHashCode()) ?? 0;
            }
        }

        [DocgenIgnore]
        public static bool operator ==(TableDescriptor obj1, TableDescriptor obj2)
        {
            if (obj1 is null || obj2 is null)
                return Object.Equals(obj1, obj2);
            return obj1.Equals(obj2);
        }

        [DocgenIgnore]
        public static bool operator !=(TableDescriptor obj1, TableDescriptor obj2)
        {
            if (obj1 is null || obj2 is null)
                return !Object.Equals(obj1, obj2);
            return !obj1.Equals(obj2);
        }

        /// <summary>
        /// The flag indicating that the table is obsolete
        /// </summary>
        public bool Obsolete { get; set; }

        [DocgenIgnore]
        public bool TryGetValue(string column, out ColumnInfo columnInfo) => mColumnsIndex.TryGetValue(column, out columnInfo);
    }

    [DocgenIgnore]
    public static class TableDescriptorArrayExtension
    {
        public static TableDescriptor Find(this TableDescriptor[] schema, string tableName)
        {
            foreach (TableDescriptor table in schema)
                if (string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase))
                    return table;
            return null;
        }

        public static TableDescriptor.ColumnInfo Find(this TableDescriptor[] schema, string tableName, string columnName)
        {
            foreach (TableDescriptor table in schema)
            {
                if (string.Equals(table.Name, tableName, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (TableDescriptor.ColumnInfo column in table)
                        if (string.Equals(column.Name, columnName, StringComparison.OrdinalIgnoreCase))
                            return column;

                    return null;
                }
            }
            return null;
        }

        public static bool Contains(this TableDescriptor[] schema, string tableName)
        {
            return Find(schema, tableName) != null;
        }

        public static bool ContainsView(this TableDescriptor[] schema, string tableName)
        {
            var f = Find(schema, tableName);
            return f?.View == true;
        }

        public static bool Contains(this TableDescriptor[] schema, string tableName, string columnName)
        {
            return Find(schema, tableName, columnName) != null;
        }
    }
}

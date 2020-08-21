using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public interface IPropertyAccessor
    {
        string Name { get; }
        Type PropertyType { get; }
        object GetValue(object thisObject);
        void SetValue(object thisObject, object value);
        T GetCustomAttribute<T>() where T : System.Attribute;
    }

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
        public T GetCustomAttribute<T>() where T : System.Attribute => mPropertyInfo.GetCustomAttribute<T>();

    }

    public class TableDescriptor : IEnumerable<TableDescriptor.ColumnInfo>, IEquatable<TableDescriptor>
    {
        public class ColumnInfo : IEquatable<ColumnInfo>
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public DbType DbType { get; set; }
            public int Size { get; set; }
            public int Precision { get; set; }
            public bool PrimaryKey { get; set; }
            public bool Autoincrement { get; set; }
            public bool Sorted { get; set; }
            public bool Unique { get; set; }
            public bool Nullable { get; set; }
            public bool ForeignKey => ForeignTable != null;
            public TableDescriptor ForeignTable { get; set; }
            public TableDescriptor Table { get; internal set; }
            //public PropertyInfo PropertyInfo { get; internal set; }
            public IPropertyAccessor PropertyAccessor { get; internal set; }
            public bool IgnoreRead { get; set; }
            public object DefaultValue { get; set; }

            private string mFullName = null;

            public string FullName
            {
                get
                {
                    if (mFullName == null)
                        mFullName = $"{Table.Name}.{Name}";
                    return mFullName;
                }
            }

            public bool Equals(ColumnInfo other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(FullName, other.FullName);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ColumnInfo) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return FullName.GetHashCode();
                }
            }

            public static bool operator == (ColumnInfo obj1, ColumnInfo obj2)
            {
                if ((object)obj1 == null || (object)obj2 == null)
                    return Object.Equals(obj1, obj2);
                else
                    return obj1.Equals(obj2);

            }

            public static bool operator != (ColumnInfo obj1, ColumnInfo obj2)
            {
                if ((object)obj1 == null || (object)obj2 == null)
                    return !Object.Equals(obj1, obj2);
                else
                    return !obj1.Equals(obj2);

            }
        }

        private List<ColumnInfo> mColumns = new List<ColumnInfo>();

        public string Name { get; set; }

        public string Scope { get; set; }

        public ColumnInfo PrimaryKey { get; private set; } = null;

        public int Count => mColumns.Count;

        public ColumnInfo this[int index] => mColumns[index];

        class ColumnIndexComparer : IEqualityComparer<string>
        {
            public bool Equals(string x, string y)
            {
                if (x == null && y == null)
                    return true;
                if (x == null || y == null)
                    return false;
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase) == 0;
            }

            public int GetHashCode(string obj)
            {
                if (obj == null)
                    return 0;
                return obj.GetHashCode();
            }
        }

        private Dictionary<string, ColumnInfo> mColumnsIndex = new Dictionary<string, ColumnInfo>(new ColumnIndexComparer());

        public ColumnInfo this[string name] => mColumnsIndex[name];

        public TableDescriptor()
        {

        }

        public TableDescriptor(string name)
        {
            Name = name;
        }

        public TableDescriptor(string name, IEnumerable<ColumnInfo> columns)
        {
            Name = name;
            Add(columns);
        }

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

        public void Add(IEnumerable<ColumnInfo> columns)
        {
            if (columns == null)
                throw new ArgumentNullException(nameof(columns));
            foreach (ColumnInfo column in columns)
                Add(column);
        }

        public IEnumerator<ColumnInfo> GetEnumerator()
        {
            return mColumns.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) mColumns).GetEnumerator();
        }

        public bool Equals(TableDescriptor other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TableDescriptor) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return Name == null ? 0 : Name.GetHashCode();
            }
        }

        public static bool operator == (TableDescriptor obj1, TableDescriptor obj2)
        {
            if ((object)obj1 == null || (object)obj2 == null)
                return Object.Equals(obj1, obj2);
            return obj1.Equals(obj2);
        }

        public static bool operator != (TableDescriptor obj1, TableDescriptor obj2)
        {
            if ((object)obj1 == null || (object)obj2 == null)
                return !Object.Equals(obj1, obj2);
            return !obj1.Equals(obj2);
        }

        public bool Obsolete { get; set; }
    }

    public static class TableDescriptorArrayExtension
    {
        public static TableDescriptor Find(this TableDescriptor[] schema, string tableName)
        {
            foreach (TableDescriptor table in schema)
                if (string.Compare(table.Name, tableName, StringComparison.OrdinalIgnoreCase) == 0)
                    return table;
            return null;
        }

        public static TableDescriptor.ColumnInfo Find(this TableDescriptor[] schema, string tableName, string columnName)
        {
            foreach (TableDescriptor table in schema)
            {
                if (string.Compare(table.Name, tableName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    foreach (TableDescriptor.ColumnInfo column in table)
                        if (string.Compare(column.Name, columnName, StringComparison.OrdinalIgnoreCase) == 0)
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


        public static bool Contains(this TableDescriptor[] schema, string tableName, string columnName)
        {
            return Find(schema, tableName, columnName) != null;
        }
    }
}

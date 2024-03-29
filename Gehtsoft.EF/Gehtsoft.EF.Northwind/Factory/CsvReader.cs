﻿using Gehtsoft.EF.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Northwind.Factory
{
    internal class CsvReader<T>
        where T : class, new()
    {
        private readonly int? mMaxRecords;

        public CsvReader(int? maxRecords = null)
        {
            mMaxRecords = maxRecords;
        }

        private class ColumnInfo
        {
            public EntityPropertyAttribute Attribute { get; set; }
            public PropertyInfo Property { get; set; }
            public ColumnInfo Reference { get; set; }
        }

        public IReadOnlyList<T> Read()
        {
            EntityAttribute typeAttribute = typeof(T).GetCustomAttribute<EntityAttribute>();
            if (typeAttribute == null)
                throw new InvalidOperationException($"Type T({typeof(T).FullName}) is not an entity type");

            Dictionary<string, ColumnInfo> properties = new Dictionary<string, ColumnInfo>();
            foreach (PropertyInfo propertyInfo in typeof(T).GetProperties())
            {
                EntityPropertyAttribute propertyAttribute = propertyInfo.GetCustomAttribute<EntityPropertyAttribute>();
                if (propertyAttribute != null)
                {
                    properties[propertyAttribute.Field ?? propertyInfo.Name] = new ColumnInfo() { Attribute = propertyAttribute, Property = propertyInfo };
                }
            }

            List<T> res = new List<T>();

            using (Stream s = typeof(T).Assembly.GetManifestResourceStream($"{typeof(T).Namespace}.csv.{typeAttribute.Table.Substring(3)}.csv"))
            {
                using (TextReader reader = new StreamReader(s, Encoding.UTF8, true))
                {
                    string line;
                    ColumnInfo[] columns = null;

                    while ((line = reader.ReadLine()) != null && (mMaxRecords == null || res.Count < mMaxRecords.Value))
                    {
                        string[] values = line.Split(',');
                        if (columns == null)
                        {
                            columns = new ColumnInfo[values.Length];
                            for (int i = 0; i < values.Length; i++)
                                if (!properties.TryGetValue(values[i], out columns[i]))
                                    columns[i] = null;
                        }
                        else
                        {
                            object target = Activator.CreateInstance(typeof(T));

                            for (int i = 0; i < columns.Length && i < values.Length; i++)
                            {
                                ColumnInfo column = columns[i];
                                if (column == null)
                                    continue;

                                ProcessValue(values[i], target, column);
                            }

                            res.Add((T)target);
                        }
                    }
                }
            }
            return res;
        }

        private void ProcessValue(string value, object target, ColumnInfo column)
        {
            object data;
            if (column.Attribute.Nullable && value == "NULL")
                data = null;
            else if (column.Attribute.ForeignKey)
            {
                if (column.Reference == null)
                {
                    foreach (PropertyInfo propertyInfo in column.Property.PropertyType.GetProperties())
                    {
                        EntityPropertyAttribute propertyAttribute = propertyInfo.GetCustomAttribute<EntityPropertyAttribute>();
                        if (propertyAttribute != null && (propertyAttribute.PrimaryKey || propertyAttribute.AutoId))
                        {
                            column.Reference = new ColumnInfo() { Attribute = propertyAttribute, Property = propertyInfo };
                            break;
                        }
                    }
                    if (column.Reference == null)
                        throw new InvalidOperationException($"Cannot find primary key for foreign key in {column.Property.DeclaringType.Name}.{column.Property.Name}");
                }
                data = Activator.CreateInstance(column.Property.PropertyType);
                ProcessValue(value, data, column.Reference);
            }
            else if (column.Property.PropertyType == typeof(string))
            {
                data = value;
            }
            else if (column.Property.PropertyType == typeof(int) || column.Property.PropertyType == typeof(int?))
            {
                data = Int32.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (column.Property.PropertyType == typeof(bool) || column.Property.PropertyType == typeof(bool?))
            {
                data = Int32.Parse(value, CultureInfo.InvariantCulture) != 0;
            }
            else if (column.Property.PropertyType == typeof(double) || column.Property.PropertyType == typeof(double?))
            {
                data = Double.Parse(value, CultureInfo.InvariantCulture);
            }
            else if (column.Property.PropertyType == typeof(DateTime) || column.Property.PropertyType == typeof(DateTime?))
            {
                data = DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            else
                throw new InvalidCastException($"Type {column.Property.PropertyType.FullName} of {column.Property.DeclaringType.Name}.{column.Property.Name} is not supported");

            column.Property.SetValue(target, data);
        }
    }
}
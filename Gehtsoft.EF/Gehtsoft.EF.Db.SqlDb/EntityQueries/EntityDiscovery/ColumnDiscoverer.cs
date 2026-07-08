using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class ColumnDiscoverer
    {
        protected void CreateColumnDescriptor(Type type, AllEntities entities, EntityNamingPolicy policy, TableDescriptor descriptor, IPropertyAccessor propertyAccessor)
        {
            JsonEntityPropertyAttribute jsonAttribute = propertyAccessor.GetCustomAttribute<JsonEntityPropertyAttribute>();
            if (jsonAttribute != null)
            {
                CreateJsonColumnDescriptor(policy, descriptor, propertyAccessor, jsonAttribute);
                return;
            }

            EntityPropertyAttribute propertyAttribute = propertyAccessor.GetCustomAttribute<EntityPropertyAttribute>();
            if (propertyAttribute != null)
            {
                if (policy == EntityNamingPolicy.BackwardCompatibility && propertyAttribute.Field == null)
                    propertyAttribute.Field = propertyAccessor.Name.ToLower();

                if (propertyAttribute.ForeignKey)
                {
                    TableDescriptor foreignTable = propertyAccessor.PropertyType == type ? descriptor : entities[propertyAccessor.PropertyType].TableDescriptor;
                    TableDescriptor.ColumnInfo pk = foreignTable.PrimaryKey;

                    descriptor.Add(new TableDescriptor.ColumnInfo()
                    {
                        ID = propertyAccessor.Name,
                        Name = propertyAttribute.Field ?? EntityNameConvertor.ConvertName(foreignTable.Name + "Ref", policy),
                        DbType = pk.DbType,
                        PrimaryKey = false,
                        Autoincrement = false,
                        Nullable = propertyAttribute.Nullable,
                        Size = pk.Size,
                        Precision = pk.Precision,
                        Sorted = propertyAttribute.Sorted,
                        ForeignTable = foreignTable,
                        PropertyAccessor = propertyAccessor,
                        DefaultValue = propertyAttribute.DefaultValue,
                    });
                }
                else if (propertyAttribute.AutoId)
                {
                    descriptor.Add(new TableDescriptor.ColumnInfo()
                    {
                        ID = propertyAccessor.Name,
                        Name = propertyAttribute.Field ?? EntityNameConvertor.ConvertName("Id", policy),
                        DbType = System.Data.DbType.Int32,
                        PrimaryKey = true,
                        Autoincrement = true,
                        Nullable = false,
                        Size = 0,
                        Precision = 0,
                        ForeignTable = null,
                        PropertyAccessor = propertyAccessor,
                        IgnoreRead = propertyAttribute.IgnoreRead,
                        DefaultValue = propertyAttribute.DefaultValue,
                    });
                }
                else
                {
                    if (propertyAttribute.DbType == DbType.Object)
                    {
                        bool nullable = false;

                        Type propType = propertyAccessor.PropertyType;
                        Type propType1 = Nullable.GetUnderlyingType(propType);

                        if (propType1 != null)
                        {
                            propType = propType1;
                            nullable = true;
                        }

                        if (propType == typeof(string))
                        {
                            propertyAttribute.DbType = DbType.String;
                        }
                        else if (propType == typeof(Guid))
                        {
                            propertyAttribute.DbType = DbType.Guid;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(bool))
                        {
                            propertyAttribute.DbType = DbType.Boolean;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(short))
                        {
                            propertyAttribute.DbType = DbType.Int16;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(int))
                        {
                            propertyAttribute.DbType = DbType.Int32;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(long))
                        {
                            propertyAttribute.DbType = DbType.Int64;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(double))
                        {
                            propertyAttribute.DbType = DbType.Double;
                            propertyAttribute.Nullable = nullable;
                            if (propertyAttribute.Size == 0)
                            {
                                propertyAttribute.Size = 18;
                                if (propertyAttribute.Precision == 0)
                                    propertyAttribute.Precision = 7;
                            }
                        }
                        else if (propType == typeof(float))
                        {
                            propertyAttribute.DbType = DbType.Single;
                            propertyAttribute.Nullable = nullable;
                            if (propertyAttribute.Size == 0)
                            {
                                propertyAttribute.Size = 18;
                                if (propertyAttribute.Precision == 0)
                                    propertyAttribute.Precision = 7;
                            }
                        }
                        else if (propType == typeof(decimal))
                        {
                            propertyAttribute.DbType = DbType.Decimal;
                            propertyAttribute.Nullable = nullable;
                            if (propertyAttribute.Size == 0)
                            {
                                propertyAttribute.Size = 18;
                                if (propertyAttribute.Precision == 0)
                                    propertyAttribute.Precision = 4;
                            }
                        }
                        else if (propType == typeof(DateTime))
                        {
                            propertyAttribute.DbType = DbType.DateTime;
                            propertyAttribute.Nullable = nullable;
                        }
                        else if (propType == typeof(byte[]))
                        {
                            propertyAttribute.DbType = DbType.Binary;
                            propertyAttribute.Nullable = true;
                        }
                    }

                    descriptor.Add(new TableDescriptor.ColumnInfo()
                    {
                        ID = propertyAccessor.Name,
                        Name = propertyAttribute.Field ?? EntityNameConvertor.ConvertName(propertyAccessor.Name, policy),
                        DbType = propertyAttribute.DbType,
                        PrimaryKey = propertyAttribute.PrimaryKey,
                        Autoincrement = propertyAttribute.Autoincrement,
                        Nullable = propertyAttribute.Nullable,
                        Size = propertyAttribute.Size,
                        Precision = propertyAttribute.Precision,
                        Sorted = propertyAttribute.Sorted,
                        ForeignTable = null,
                        PropertyAccessor = propertyAccessor,
                        IgnoreRead = propertyAttribute.IgnoreRead,
                        DefaultValue = propertyAttribute.DefaultValue,
                        Unique = propertyAttribute.Unique,
                    });
                }
            }
        }

        private void CreateJsonColumnDescriptor(EntityNamingPolicy policy, TableDescriptor descriptor, IPropertyAccessor propertyAccessor, JsonEntityPropertyAttribute attribute)
        {
            string columnName = attribute.Field ?? EntityNameConvertor.ConvertName(propertyAccessor.Name, policy);

            JsonIndexAttribute[] indexAttributes = propertyAccessor.GetCustomAttributes<JsonIndexAttribute>();
            var indexes = new List<JsonIndexDefinition>(indexAttributes.Length);
            for (int i = 0; i < indexAttributes.Length; i++)
            {
                JsonIndexAttribute ia = indexAttributes[i];
                // The index name is ALWAYS derived (never user-supplied): it encodes column + path +
                // target type, so that changing the path or the type changes the name — which lets
                // UpdateTables detect the change as a drop+create (JSON indexes are expression
                // indexes, for which structural change-detection is name-based).
                string name = DeriveJsonIndexName(columnName, ia.Path, ia.DbType);
                indexes.Add(new JsonIndexDefinition(ia.Path, ia.DbType, ia.Unique, name));
            }

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = propertyAccessor.Name,
                Name = columnName,
                DbType = DbType.String,
                Size = 0,
                Nullable = attribute.Nullable,
                ForeignTable = null,
                PropertyAccessor = new JsonPropertyAccessor(propertyAccessor),
                Json = new JsonColumnMetadata(propertyAccessor.PropertyType, indexes),
            });
        }

        // e.g. column "profile", "$.address.zip", Int32 -> "profile_address_zip_i32"
        private static string DeriveJsonIndexName(string columnName, string path, DbType dbType)
        {
            var builder = new StringBuilder();
            builder.Append(columnName.ToLowerInvariant());
            AppendSlug(builder, path);
            builder.Append('_').Append(JsonTypeCode(dbType));
            return builder.ToString();
        }

        private static void AppendSlug(StringBuilder builder, string path)
        {
            bool any = false;
            if (builder.Length > 0)
                builder.Append('_');
            int start = builder.Length;
            if (!string.IsNullOrEmpty(path))
            {
                for (int i = 0; i < path.Length; i++)
                {
                    char c = path[i];
                    if (char.IsLetterOrDigit(c))
                    {
                        builder.Append(char.ToLowerInvariant(c));
                        any = true;
                    }
                    else if (builder.Length > start && builder[builder.Length - 1] != '_')
                    {
                        builder.Append('_');
                    }
                }
            }
            while (builder.Length > start && builder[builder.Length - 1] == '_')
                builder.Length--;
            if (!any)
                builder.Append("root");
        }

        private static string JsonTypeCode(DbType dbType)
        {
            switch (dbType)
            {
                case DbType.String:
                case DbType.AnsiString:
                case DbType.StringFixedLength:
                case DbType.AnsiStringFixedLength:
                    return "str";
                case DbType.Boolean:
                    return "bool";
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.Date:
                    return "dt";
                case DbType.Int16:
                    return "i16";
                case DbType.Int32:
                    return "i32";
                case DbType.Int64:
                    return "i64";
                case DbType.Double:
                    return "dbl";
                case DbType.Single:
                    return "sgl";
                case DbType.Decimal:
                case DbType.Currency:
                    return "dec";
                default:
                    return ((int)dbType).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}

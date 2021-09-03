using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class ColumnDiscoverer
    {
        protected void CreateColumnDescriptor(Type type, AllEntities entities, EntityNamingPolicy policy, TableDescriptor descriptor, IPropertyAccessor propertyAccessor)
        {
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
    }
}

using System;
using System.Globalization;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Serialization.IO
{
    /// <summary>
    /// Shared logic for rebuilding an entity instance from deserialized column values.
    /// Used by the Binary and JSON readers; mirrors the behaviour of the XML reader
    /// (apply default values, resolve foreign keys to stub instances carrying only the
    /// primary key, honour enums and nullable types).
    /// </summary>
    internal static class EntityMaterializer
    {
        public static object CreateInstance(EntityDescriptor descriptor)
        {
            object entity = Activator.CreateInstance(descriptor.EntityType);
            var table = descriptor.TableDescriptor;
            for (int i = 0; i < table.Count; i++)
            {
                if (table[i].DefaultValue != null)
                    table[i].PropertyAccessor.SetValue(entity, table[i].DefaultValue);
            }
            return entity;
        }

        public static void Assign(object entity, TableDescriptor.ColumnInfo column, object raw)
        {
            if (column.ForeignKey)
            {
                if (raw == null)
                {
                    column.PropertyAccessor.SetValue(entity, null);
                }
                else
                {
                    EntityDescriptor foreignDescriptor = AllEntities.Inst[column.PropertyAccessor.PropertyType];
                    object pk = ConvertValue(raw, foreignDescriptor.PrimaryKey.PropertyAccessor.PropertyType);
                    object stub = Activator.CreateInstance(foreignDescriptor.EntityType);
                    foreignDescriptor.PrimaryKey.PropertyAccessor.SetValue(stub, pk);
                    column.PropertyAccessor.SetValue(entity, stub);
                }
            }
            else
            {
                column.PropertyAccessor.SetValue(entity, ConvertValue(raw, column.PropertyAccessor.PropertyType));
            }
        }

        /// <summary>
        /// Converts an already-typed scalar (as produced by the binary/text codecs) to the
        /// target property type, unwrapping Nullable&lt;&gt; and converting enums.
        /// </summary>
        public static object ConvertValue(object value, Type type)
        {
            TypeInfo typeInfo = type.GetTypeInfo();

            if (value == null)
                return typeInfo.IsValueType ? Activator.CreateInstance(type) : null;

            Type underlying = Nullable.GetUnderlyingType(type) ?? type;
            TypeInfo underlyingInfo = underlying.GetTypeInfo();

            if (underlyingInfo.IsEnum)
                return Enum.ToObject(underlying, value);

            if (value.GetType() == underlying)
                return value;

            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
        }
    }
}

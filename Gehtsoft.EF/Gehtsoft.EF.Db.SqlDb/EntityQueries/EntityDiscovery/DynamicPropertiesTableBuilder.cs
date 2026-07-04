using System.Collections.Generic;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Synthesizes the EAV side-table descriptor for an entity that owns a dynamic property set.
    /// </summary>
    internal static class DynamicPropertiesTableBuilder
    {
        internal const string IdColumn = "id";
        internal const string OwnerColumn = "owner";
        internal const string NameColumn = "name";
        internal const string PropTypeColumn = "prop_type";
        internal const string StringValueColumn = "v_str";
        internal const string IntValueColumn = "v_int";
        internal const string RealValueColumn = "v_real";

        internal const string OwnerNameIndex = "owner_name";
        internal const string NameStringIndex = "name_str";
        internal const string NameIntIndex = "name_int";
        internal const string NameRealIndex = "name_real";

        private sealed class CompositeIndexMetadata : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes { get; }

            public CompositeIndexMetadata(IEnumerable<CompositeIndex> indexes)
            {
                Indexes = indexes;
            }
        }

        /// <summary>
        /// Builds the EAV side-table descriptor for the specified owner entity.
        /// </summary>
        /// <param name="owner">The descriptor of the entity that owns the dynamic property set.</param>
        /// <returns>The synthesized side-table descriptor.</returns>
        public static TableDescriptor Build(EntityDescriptor owner)
            => Build(owner.TableDescriptor, owner.DynamicProperties);

        /// <summary>
        /// Builds the EAV side-table descriptor for the specified owner table.
        ///
        /// The side table layout is fixed; the <paramref name="options"/> only tune column
        /// sizes. Pass `null` to use the defaults - useful when the owning entity no longer
        /// carries <see cref="DynamicPropertiesAttribute"/> but its side table still has to be
        /// located (e.g. to drop it during a schema update). The table name is always
        /// `&lt;owner&gt;<see cref="DynamicPropertiesAttribute.TableSuffix"/>`, independent of the options.
        /// </summary>
        /// <param name="ownerTable">The descriptor of the owner table.</param>
        /// <param name="options">The shaping options, or `null` for the defaults.</param>
        /// <returns>The synthesized side-table descriptor.</returns>
        public static TableDescriptor Build(TableDescriptor ownerTable, DynamicPropertiesAttribute options)
        {
            TableDescriptor.ColumnInfo ownerPk = ownerTable.PrimaryKey;
            if (ownerPk == null)
                throw new EfSqlException(EfExceptionCode.NoPrimaryKeyInTable, ownerTable.Name);

            if (options == null)
                options = new DynamicPropertiesAttribute();

            TableDescriptor descriptor = new TableDescriptor(ownerTable.Name + DynamicPropertiesAttribute.TableSuffix);

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "Id",
                Name = IdColumn,
                DbType = DbType.Int64,
                PrimaryKey = true,
                Autoincrement = true,
                Nullable = false,
            });

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "Owner",
                Name = OwnerColumn,
                DbType = ownerPk.DbType,
                Size = ownerPk.Size,
                Precision = ownerPk.Precision,
                Nullable = false,
                ForeignTable = ownerTable,
            });

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "Name",
                Name = NameColumn,
                DbType = DbType.String,
                Size = options.NameSize,
                Nullable = false,
            });

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "PropType",
                Name = PropTypeColumn,
                DbType = DbType.Int32,
                Nullable = false,
            });

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "StringValue",
                Name = StringValueColumn,
                DbType = DbType.String,
                Size = options.StringValueSize,
                Nullable = true,
            });

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "IntValue",
                Name = IntValueColumn,
                DbType = DbType.Int64,
                Nullable = true,
            });

            descriptor.Add(new TableDescriptor.ColumnInfo()
            {
                ID = "RealValue",
                Name = RealValueColumn,
                DbType = DbType.Double,
                Size = options.RealValueSize < 0 ? 0 : options.RealValueSize,
                Precision = options.RealValuePrecision < 0 ? 0 : options.RealValuePrecision,
                Nullable = true,
            });

            descriptor.Metadata = new CompositeIndexMetadata(new[]
            {
                MakeIndex(OwnerNameIndex, OwnerColumn, NameColumn),
                MakeIndex(NameStringIndex, NameColumn, StringValueColumn),
                MakeIndex(NameIntIndex, NameColumn, IntValueColumn),
                MakeIndex(NameRealIndex, NameColumn, RealValueColumn),
            });

            return descriptor;
        }

        private static CompositeIndex MakeIndex(string name, params string[] columns)
        {
            CompositeIndex index = new CompositeIndex(name);
            foreach (string column in columns)
                index.Add(column);
            return index;
        }
    }
}

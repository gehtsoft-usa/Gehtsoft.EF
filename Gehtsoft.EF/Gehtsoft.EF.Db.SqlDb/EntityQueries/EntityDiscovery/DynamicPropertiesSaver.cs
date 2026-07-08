using System.Data;
using System.Globalization;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Query-agnostic helpers shared by the entity queries that persist a dynamic property bag:
    /// bag access/guards, and the building blocks for the combined multi-statement command that
    /// writes property-row changes. The per-operation orchestration (which changes to apply, when
    /// to accept them) lives in the respective entity query classes (insert, update, ...).
    ///
    /// Combined-command convention: every property change is one sub-query added to a
    /// <see cref="MultiSqlQueryBuilder"/>; the owner PK is a single shared `owner` parameter (bound
    /// once via <see cref="BindOwner"/>), and each change's own columns use row-suffixed parameters.
    /// </summary>
    internal static class DynamicPropertiesSaver
    {
        /// <summary>
        /// Returns the entity's dynamic property bag, or `null` when there is nothing to persist
        /// (the entity does not own dynamic properties, or it has no bag). `null` means "nothing
        /// to do", not a failure.
        /// </summary>
        public static DynamicPropertyBag GetBag(EntityDescriptor descriptor, object entity)
        {
            if (!descriptor.HasDynamicProperties)
                return null;
            return (entity as IDynamicPropertiesOwner)?.DynamicProperties;
        }

        /// <summary>
        /// Throws if the bag is not a new bag (a bag may only be persisted by an insert when it is new).
        /// </summary>
        public static void RequireNewBag(DynamicPropertyBag bag)
        {
            if (!bag.IsNew)
                throw new EfSqlException(EfExceptionCode.DynamicPropertiesBagIsNotNew);
        }

        /// <summary>
        /// Throws if the bag is a new bag (a new bag can only be inserted; an update needs a
        /// persisted/loaded baseline to compute the net changes against).
        /// </summary>
        public static void RequireExistingBag(DynamicPropertyBag bag)
        {
            if (bag.IsNew)
                throw new EfSqlException(EfExceptionCode.DynamicPropertiesBagIsNew);
        }

        public static string Suffixed(string column, int row) => column + "_" + row.ToString(CultureInfo.InvariantCulture);

        /// <summary>Adds an INSERT of one property row (no id read-back; owner shared, columns row-suffixed).</summary>
        public static void AddInsert(MultiSqlQueryBuilder multi, SqlDbConnection connection, TableDescriptor propsTable, int row)
        {
            InsertQueryBuilder insert = connection.GetInsertQueryBuilder(propsTable);
            // The generated property-row id is never used, and the per-row read-back tail
            // ("; SELECT LAST_INSERT_ID();" / "RETURNING id INTO :id") breaks the combined command
            // on MySQL/Oracle - so suppress it.
            insert.ReturnAutoincrement = false;
            insert.SetParameterNames(
                (DynamicPropertiesTableBuilder.NameColumn, Suffixed(DynamicPropertiesTableBuilder.NameColumn, row)),
                (DynamicPropertiesTableBuilder.PropTypeColumn, Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row)),
                (DynamicPropertiesTableBuilder.StringValueColumn, Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row)),
                (DynamicPropertiesTableBuilder.IntValueColumn, Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row)),
                (DynamicPropertiesTableBuilder.RealValueColumn, Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row)));
            multi.Add(insert);
        }

        /// <summary>Adds an UPDATE of one property row: SET the value columns WHERE owner=@owner AND name=@name_row.</summary>
        public static void AddUpdate(MultiSqlQueryBuilder multi, SqlDbConnection connection, TableDescriptor propsTable, int row)
        {
            UpdateQueryBuilder update = connection.GetUpdateQueryBuilder(propsTable);
            update.AddUpdateColumn(propsTable[DynamicPropertiesTableBuilder.PropTypeColumnId], Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row));
            update.AddUpdateColumn(propsTable[DynamicPropertiesTableBuilder.StringValueColumnId], Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row));
            update.AddUpdateColumn(propsTable[DynamicPropertiesTableBuilder.IntValueColumnId], Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row));
            update.AddUpdateColumn(propsTable[DynamicPropertiesTableBuilder.RealValueColumnId], Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row));
            update.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId]).Is(CmpOp.Eq).Parameter(DynamicPropertiesTableBuilder.OwnerColumn);
            update.Where.Property(propsTable[DynamicPropertiesTableBuilder.NameColumnId]).Is(CmpOp.Eq).Parameter(Suffixed(DynamicPropertiesTableBuilder.NameColumn, row));
            multi.Add(update);
        }

        /// <summary>Adds a DELETE of one property row: WHERE owner=@owner AND name=@name_row.</summary>
        public static void AddDelete(MultiSqlQueryBuilder multi, SqlDbConnection connection, TableDescriptor propsTable, int row)
        {
            DeleteQueryBuilder delete = connection.GetDeleteQueryBuilder(propsTable);
            delete.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId]).Is(CmpOp.Eq).Parameter(DynamicPropertiesTableBuilder.OwnerColumn);
            delete.Where.Property(propsTable[DynamicPropertiesTableBuilder.NameColumnId]).Is(CmpOp.Eq).Parameter(Suffixed(DynamicPropertiesTableBuilder.NameColumn, row));
            multi.Add(delete);
        }

        /// <summary>Binds the single shared owner parameter (referenced by every sub-query).</summary>
        public static void BindOwner(SqlDbQuery query, object ownerPk)
            => query.BindParam(DynamicPropertiesTableBuilder.OwnerColumn, ownerPk.GetType(), ownerPk);

        /// <summary>Binds one property row's name, type and value (for an INSERT or UPDATE sub-query).</summary>
        public static void BindValueRow(SqlDbQuery query, int row, string name, object value)
        {
            (DynamicPropertyValueType type, string column, object encoded) = DynamicPropertiesValueMapper.Encode(value);
            query.BindParam<string>(Suffixed(DynamicPropertiesTableBuilder.NameColumn, row), name);
            query.BindParam<int>(Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row), (int)type);
            BindValueColumns(query,
                Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row),
                Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row),
                Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row),
                column, encoded);
        }

        /// <summary>Binds one property row's name only (for a DELETE sub-query).</summary>
        public static void BindNameRow(SqlDbQuery query, int row, string name)
            => query.BindParam<string>(Suffixed(DynamicPropertiesTableBuilder.NameColumn, row), name);

        // ---- shared by the multi-row (by-condition) operations: MultiDelete / MultiUpdate ----

        /// <summary>Whether the rendered owner WHERE references the side table (i.e. filters on a dynamic property).</summary>
        public static bool ConditionReferencesProps(EntityDescriptor descriptor, ConditionBuilder ownerWhere)
            => ownerWhere.ToString().Contains(descriptor.DynamicPropertiesTable.Name);

        /// <summary>
        /// SELECT &lt;owner&gt;.&lt;pk&gt; FROM &lt;owner&gt; WHERE &lt;the modify statement's WHERE, realigned to
        /// this select's alias scheme&gt;. The modify statement qualifies columns as "&lt;table&gt;.&lt;col&gt;",
        /// the select as "&lt;entityN&gt;.&lt;col&gt;", so the qualifier prefix is rewritten (no SQL is emitted -
        /// only two builder fragments are reconciled).
        /// </summary>
        public static SelectQueryBuilder BuildMatchedIdsSelect(SqlDbConnection connection, EntityDescriptor descriptor, ConditionBuilder ownerWhere)
        {
            TableDescriptor ownerTable = descriptor.TableDescriptor;
            TableDescriptor.ColumnInfo pk = descriptor.PrimaryKey;

            SelectQueryBuilder idsSelect = connection.GetSelectQueryBuilder(ownerTable);
            idsSelect.AddToResultset(pk);
            idsSelect.Where.Add(LogOp.And, RealignQualifier(ownerWhere.ToString(), ownerTable.Name, OwnerAliasPrefix(idsSelect, pk)));
            return idsSelect;
        }

        private static string RealignQualifier(string whereText, string fromPrefix, string toPrefix)
            => whereText.Replace(fromPrefix + ".", toPrefix + ".");

        private static string OwnerAliasPrefix(SelectQueryBuilder select, TableDescriptor.ColumnInfo pk)
        {
            string reference = select.GetAlias(pk, null); // "<entityN>.<pk>"
            int dot = reference.IndexOf('.');
            return dot < 0 ? reference : reference.Substring(0, dot);
        }

        /// <summary>
        /// Adds a "bulk" INSERT of one property row where <b>every</b> column is row-suffixed - including
        /// owner. Used to set a property for many different owners in one command (MultiUpdate), where the
        /// owner differs per row (unlike the single-entity insert, where owner is a shared parameter).
        /// </summary>
        public static void AddBulkInsert(MultiSqlQueryBuilder multi, SqlDbConnection connection, TableDescriptor propsTable, int row)
        {
            InsertQueryBuilder insert = connection.GetInsertQueryBuilder(propsTable);
            insert.ReturnAutoincrement = false;
            insert.SetParameterNames(
                (DynamicPropertiesTableBuilder.OwnerColumn, Suffixed(DynamicPropertiesTableBuilder.OwnerColumn, row)),
                (DynamicPropertiesTableBuilder.NameColumn, Suffixed(DynamicPropertiesTableBuilder.NameColumn, row)),
                (DynamicPropertiesTableBuilder.PropTypeColumn, Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row)),
                (DynamicPropertiesTableBuilder.StringValueColumn, Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row)),
                (DynamicPropertiesTableBuilder.IntValueColumn, Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row)),
                (DynamicPropertiesTableBuilder.RealValueColumn, Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row)));
            multi.Add(insert);
        }

        /// <summary>Binds a bulk INSERT row: owner, name, type and value (all row-suffixed).</summary>
        public static void BindBulkInsert(SqlDbQuery query, int row, object ownerPk, string name, object value)
        {
            (DynamicPropertyValueType type, string column, object encoded) = DynamicPropertiesValueMapper.Encode(value);
            query.BindParam(Suffixed(DynamicPropertiesTableBuilder.OwnerColumn, row), ownerPk.GetType(), ownerPk);
            query.BindParam<string>(Suffixed(DynamicPropertiesTableBuilder.NameColumn, row), name);
            query.BindParam<int>(Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row), (int)type);
            BindValueColumns(query,
                Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row),
                Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row),
                Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row),
                column, encoded);
        }

        /// <summary>
        /// Binds one property's value into the three value columns of the side table: the column
        /// selected by <paramref name="valueColumn"/> gets the encoded value, the other two are
        /// bound to null. The caller supplies the parameter names (they differ per operation - e.g.
        /// row-suffixed for a multi-row insert).
        /// </summary>
        public static void BindValueColumns(SqlDbQuery query, string stringParameter, string intParameter, string realParameter, string valueColumn, object encoded)
        {
            BindColumn(query, stringParameter, DbType.String, valueColumn == DynamicPropertiesTableBuilder.StringValueColumn, encoded);
            BindColumn(query, intParameter, DbType.Int64, valueColumn == DynamicPropertiesTableBuilder.IntValueColumn, encoded);
            BindColumn(query, realParameter, DbType.Double, valueColumn == DynamicPropertiesTableBuilder.RealValueColumn, encoded);
        }

        private static void BindColumn(SqlDbQuery query, string parameter, DbType type, bool isTarget, object encoded)
        {
            if (isTarget)
                query.BindParam(parameter, type, encoded);
            else
                query.BindNull(parameter, type);
        }
    }
}

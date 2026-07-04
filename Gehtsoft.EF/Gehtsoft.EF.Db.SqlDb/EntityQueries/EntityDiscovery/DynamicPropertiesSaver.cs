using System.Data;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Query-agnostic helpers shared by the entity queries that persist a dynamic property bag.
    /// The operation-specific query building/execution lives in the respective entity query
    /// classes (insert, delete, ...), not here.
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

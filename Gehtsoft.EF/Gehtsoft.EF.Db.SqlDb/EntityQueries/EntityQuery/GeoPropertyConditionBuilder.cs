using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Extension methods that start a condition on a geometry property of an entity: a topological /
    /// within-distance predicate (<c>GeoPredicateOf</c>) or a geometry scalar to compare (<c>GeoScalarOf</c>).
    /// The operand geometry is supplied as WKB (<c>byte[]</c>) - the ergonomic overloads that take an
    /// NetTopologySuite object live in the <c>Gehtsoft.EF.Geo.NetTopologySuite</c> module, keeping the core
    /// on <c>byte[]</c>. Twin of <see cref="JsonPropertyConditionBuilderExtension"/>.
    /// </summary>
    public static class GeoPropertyConditionBuilderExtension
    {
        /// <summary>
        /// Starts a geometry predicate on a property against a bound WKB geometry, connected with logical and.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf(this EntityQueryConditionBuilder builder, string name, SqlGeoPredicateId op, byte[] operandWkb, Type entityType = null, int occurrence = 0, double distance = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.GeoPredicateOf(name, op, operandWkb, entityType, occurrence, distance);
            return rc;
        }

        /// <summary>
        /// Generic version of <see cref="GeoPredicateOf(EntityQueryConditionBuilder, string, SqlGeoPredicateId, byte[], Type, int, double)"/>.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf<T>(this EntityQueryConditionBuilder builder, string name, SqlGeoPredicateId op, byte[] operandWkb, int occurrence = 0, double distance = 0)
            => builder.GeoPredicateOf(name, op, operandWkb, typeof(T), occurrence, distance);

        /// <summary>
        /// Starts a geometry predicate on a property against a native-geometry subquery, connected with logical and.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf(this EntityQueryConditionBuilder builder, string name, SqlGeoPredicateId op, AQueryBuilder nativeSubquery, Type entityType = null, int occurrence = 0, double distance = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.GeoPredicateOf(name, op, nativeSubquery, entityType, occurrence, distance);
            return rc;
        }

        /// <summary>
        /// Generic version of the native-geometry-subquery predicate.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf<T>(this EntityQueryConditionBuilder builder, string name, SqlGeoPredicateId op, AQueryBuilder nativeSubquery, int occurrence = 0, double distance = 0)
            => builder.GeoPredicateOf(name, op, nativeSubquery, typeof(T), occurrence, distance);

        /// <summary>
        /// Starts a condition on a geometry scalar (Area, Length, Distance, X, Y, ...) of a property,
        /// connected with logical and. Chain a comparison, for example <c>.Gt(500.0)</c>.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoScalarOf(this EntityQueryConditionBuilder builder, string name, SqlGeoFunctionId op, byte[] operandWkb = null, DbType resultType = DbType.Double, Type entityType = null, int occurrence = 0, double tolerance = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.GeoScalarOf(name, op, operandWkb, resultType, entityType, occurrence, tolerance);
            return rc;
        }

        /// <summary>
        /// Generic version of <see cref="GeoScalarOf(EntityQueryConditionBuilder, string, SqlGeoFunctionId, byte[], DbType, Type, int, double)"/>.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoScalarOf<T>(this EntityQueryConditionBuilder builder, string name, SqlGeoFunctionId op, byte[] operandWkb = null, DbType resultType = DbType.Double, int occurrence = 0, double tolerance = 0)
            => builder.GeoScalarOf(name, op, operandWkb, resultType, typeof(T), occurrence, tolerance);
    }
}

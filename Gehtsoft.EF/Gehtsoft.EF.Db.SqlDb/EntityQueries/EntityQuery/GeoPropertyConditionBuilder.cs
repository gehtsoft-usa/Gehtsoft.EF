using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Extension methods that start a condition on a geometry property of an entity.
    /// </summary>
    /// <remarks>
    /// A topological / within-distance predicate (<c>GeoPredicateOf</c>) or a geometry scalar to compare
    /// (<c>GeoScalarOf</c>). The operand geometry is supplied as WKB (<c>byte[]</c>) - the ergonomic
    /// overloads that take a NetTopologySuite object live in the <c>Gehtsoft.EF.Geo.NetTopologySuite</c>
    /// module, keeping the core on <c>byte[]</c>. Twin of <see cref="JsonPropertyConditionBuilderExtension"/>.
    /// </remarks>
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

        /// <summary>Generic version of the WKB-operand geometry predicate (the entity type is the generic argument).</summary>
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
        /// Starts a condition on a geometry scalar (Area, Length, Distance, X, Y) of a property, connected with logical and.
        /// </summary>
        /// <remarks>
        /// Chain a comparison, for example <c>.Gt(500.0)</c>.
        /// </remarks>
        public static SingleEntityQueryConditionBuilder GeoScalarOf(this EntityQueryConditionBuilder builder, string name, SqlGeoFunctionId op, byte[] operandWkb = null, DbType resultType = DbType.Double, Type entityType = null, int occurrence = 0, double tolerance = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.GeoScalarOf(name, op, operandWkb, resultType, entityType, occurrence, tolerance);
            return rc;
        }

        /// <summary>Generic version of the geometry-scalar condition (the entity type is the generic argument).</summary>
        public static SingleEntityQueryConditionBuilder GeoScalarOf<T>(this EntityQueryConditionBuilder builder, string name, SqlGeoFunctionId op, byte[] operandWkb = null, DbType resultType = DbType.Double, int occurrence = 0, double tolerance = 0)
            => builder.GeoScalarOf(name, op, operandWkb, resultType, typeof(T), occurrence, tolerance);
    }
}

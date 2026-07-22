using System;
using System.Data;
using System.Linq.Expressions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using NetTopologySuite.Geometries;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>
    /// Ergonomic entity-WHERE geometry conditions that take a NetTopologySuite <see cref="Geometry"/>
    /// operand directly. Each encodes the object to WKB through the NTS codec and delegates to the core
    /// <c>byte[]</c> overloads (<see cref="GeoPropertyConditionBuilderExtension"/>), so the core SQL layer
    /// never references an object geometry model. Member-expression overloads (<c>e =&gt; e.Shape</c>)
    /// resolve the property name from the expression's leaf member.
    /// </summary>
    public static class GeometryEntityConditionExtensions
    {
        private static readonly NtsGeometryCodec Codec = new NtsGeometryCodec();

        private static byte[] Wkb(Geometry operand) => operand == null ? null : Codec.ToWkb(operand, includeSrid: false);

        private static string MemberName<T>(Expression<Func<T, object>> expression)
        {
            Expression body = expression?.Body ?? throw new ArgumentNullException(nameof(expression));
            if (body is UnaryExpression unary)
                body = unary.Operand;
            if (body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException("A simple property expression (for example e => e.Shape) is expected", nameof(expression));
        }

        /// <summary>
        /// Starts a geometry predicate on a property against an NTS geometry operand, connected with logical and.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf(this EntityQueryConditionBuilder builder, string name, SqlGeoPredicateId op, Geometry operand, Type entityType = null, int occurrence = 0, double distance = 0)
            => GeoPropertyConditionBuilderExtension.GeoPredicateOf(builder, name, op, Wkb(operand), entityType, occurrence, distance);

        /// <summary>
        /// Generic version of the NTS-operand geometry predicate.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf<T>(this EntityQueryConditionBuilder builder, string name, SqlGeoPredicateId op, Geometry operand, int occurrence = 0, double distance = 0)
            => GeoPropertyConditionBuilderExtension.GeoPredicateOf(builder, name, op, Wkb(operand), typeof(T), occurrence, distance);

        /// <summary>
        /// Member-expression version of the NTS-operand geometry predicate: <c>e =&gt; e.Shape</c>.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoPredicateOf<T>(this EntityQueryConditionBuilder builder, Expression<Func<T, object>> property, SqlGeoPredicateId op, Geometry operand, int occurrence = 0, double distance = 0)
            => GeoPropertyConditionBuilderExtension.GeoPredicateOf(builder, MemberName(property), op, Wkb(operand), typeof(T), occurrence, distance);

        /// <summary>
        /// Starts a condition on a geometry scalar of a property, using an NTS geometry as the second
        /// operand of a binary measurement (for example Distance); pass <c>null</c> for a unary scalar.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoScalarOf(this EntityQueryConditionBuilder builder, string name, SqlGeoFunctionId op, Geometry operand = null, DbType resultType = DbType.Double, Type entityType = null, int occurrence = 0, double tolerance = 0)
            => GeoPropertyConditionBuilderExtension.GeoScalarOf(builder, name, op, Wkb(operand), resultType, entityType, occurrence, tolerance);

        /// <summary>
        /// Member-expression version of the NTS-operand geometry scalar: <c>e =&gt; e.Shape</c>.
        /// </summary>
        public static SingleEntityQueryConditionBuilder GeoScalarOf<T>(this EntityQueryConditionBuilder builder, Expression<Func<T, object>> property, SqlGeoFunctionId op, Geometry operand = null, DbType resultType = DbType.Double, int occurrence = 0, double tolerance = 0)
            => GeoPropertyConditionBuilderExtension.GeoScalarOf(builder, MemberName(property), op, Wkb(operand), resultType, typeof(T), occurrence, tolerance);
    }
}

using System;
using System.Linq.Expressions;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using Gehtsoft.EF.Test.Utils.DummyDb;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// DB-free tests of the NetTopologySuite-operand entity-WHERE surface
    /// (<see cref="GeometryEntityConditionExtensions"/>): each overload encodes the NTS geometry to WKB and
    /// delegates to the core byte[] condition builder, so the rendered WHERE carries the dialect's spatial
    /// SQL. Covers the entity-type and member-expression overloads, the unary (null-operand) scalar, and the
    /// member-name resolution error paths.
    /// </summary>
    public class GeometryNtsConditionTest
    {
        [Entity(Scope = "geo_nts_cond", Table = "geo_nts_cond")]
        public class GeoNtsCond
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Srid = 4326)] public byte[] Shape { get; set; }
        }

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        private static Geometry Pt() => new Point(1, 2) { SRID = 4326 };

        [Fact]
        public void GeoPredicateOf_NtsOperand_ByEntityType()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoNtsCond>();
            q.Where.GeoPredicateOf("Shape", SqlGeoPredicateId.Intersects, Pt(), typeof(GeoNtsCond));
            q.Where.ToString().Should().Contain("ST_Intersects(");
        }

        [Fact]
        public void GeoPredicateOf_NtsOperand_MemberExpression()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoNtsCond>();
            q.Where.GeoPredicateOf<GeoNtsCond>(e => e.Shape, SqlGeoPredicateId.Contains, Pt());
            q.Where.ToString().Should().Contain("ST_Contains(");
        }

        [Fact]
        public void GeoScalarOf_NtsOperand_ByName_UnaryNullOperand()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoNtsCond>();
            // no operand -> Wkb(null) exercises the null branch of the encoder
            q.Where.GeoScalarOf<GeoNtsCond>("Shape", SqlGeoFunctionId.Area).Gt(10.0);
            q.Where.ToString().Should().Contain("ST_Area(");
        }

        [Fact]
        public void GeoScalarOf_NtsOperand_MemberExpression_WithOperand()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoNtsCond>();
            q.Where.GeoScalarOf<GeoNtsCond>(e => e.Shape, SqlGeoFunctionId.Distance, Pt()).Gt(100.0);
            q.Where.ToString().Should().Contain("ST_Distance(");
        }

        [Fact]
        public void MemberExpression_Null_ThrowsArgumentNull()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoNtsCond>();
            Expression<Func<GeoNtsCond, object>> property = null;
            ((Action)(() => q.Where.GeoPredicateOf<GeoNtsCond>(property, SqlGeoPredicateId.Intersects, Pt())))
                .Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void MemberExpression_NotAMember_ThrowsArgument()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoNtsCond>();
            ((Action)(() => q.Where.GeoScalarOf<GeoNtsCond>(e => e.ToString(), SqlGeoFunctionId.Area)))
                .Should().Throw<ArgumentException>();
        }
    }
}

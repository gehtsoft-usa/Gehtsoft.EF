using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.MysqlDb;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free tests of the per-driver geometry query renderers (<see cref="SqlDbLanguageSpecifics.GeometryFunction"/>
    /// and <see cref="SqlDbLanguageSpecifics.GeometryPredicate"/>). The three grammars diverge too far to
    /// route through a shared SQL parser (SQL Server method-calls, Oracle SDO package member access), so —
    /// as with the geometry DDL-generation tests — the pure rendering function is asserted by exact string.
    /// Behavioural round-trips against a live engine arrive in the later Phase-4 increments.
    /// </summary>
    public class GeometryRenderTest
    {
        private const string A = "t.shape";
        private const string B = "@g";
        private const string P = "@p";

        private static GeoFunctionRequest Fn(SqlGeoFunctionId op, string a = A, string b = null, string parameter = null, int srid = 0, double tolerance = 0)
            => new GeoFunctionRequest(op, a, b, parameter, srid, tolerance);

        private static GeoPredicateRequest Pr(SqlGeoPredicateId op, double distance = 0, double tolerance = 0)
            => new GeoPredicateRequest(op, A, B, distance, tolerance);

        // ---- capability gate ----

        [Theory]
        [InlineData(typeof(PostgresDbLanguageSpecifics))]
        [InlineData(typeof(MssqlDbLanguageSpecifics))]
        [InlineData(typeof(OracleDbLanguageSpecifics))]
        [InlineData(typeof(MySql8LanguageSpecifics))]
        [InlineData(typeof(MariaDbLanguageSpecifics))]
        public void SupportsGeometryQuery_TrueOnSpatialDialects(Type specificsType)
        {
            var specifics = (SqlDbLanguageSpecifics)Activator.CreateInstance(specificsType);
            specifics.SupportsGeometryQuery.Should().BeTrue();
        }

        [Fact]
        public void SupportsGeometryQuery_FalseOnUnsupportedDialect_AndRenderersThrow()
        {
            using var connection = new DummySqlConnection();
            var specifics = connection.DummyDbSpecifics;
            specifics.SupportsGeometryQuery.Should().BeFalse();

            ((Action)(() => specifics.GeometryFunction(Fn(SqlGeoFunctionId.AsBinary))))
                .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
            ((Action)(() => specifics.GeometryPredicate(Pr(SqlGeoPredicateId.Intersects))))
                .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }

        // ---- PostGIS (OGC ST_*) ----

        [Fact]
        public void Postgres_Functions()
        {
            var s = new PostgresDbLanguageSpecifics();
            s.GeometryFunction(Fn(SqlGeoFunctionId.FromWkb, a: null, parameter: P, srid: 4326)).Should().Be("ST_GeomFromWKB(@p, 4326)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.AsBinary)).Should().Be("ST_AsBinary(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Distance, b: B)).Should().Be("ST_Distance(t.shape, @g)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Area)).Should().Be("ST_Area(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Length)).Should().Be("ST_Length(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Srid)).Should().Be("ST_SRID(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.GeometryType)).Should().Be("ST_GeometryType(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.IsEmpty)).Should().Be("ST_IsEmpty(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.X)).Should().Be("ST_X(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Y)).Should().Be("ST_Y(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Envelope)).Should().Be("ST_Envelope(t.shape)");
        }

        [Fact]
        public void Postgres_Predicates()
        {
            var s = new PostgresDbLanguageSpecifics();
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Intersects)).Should().Be("ST_Intersects(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Disjoint)).Should().Be("ST_Disjoint(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Equals)).Should().Be("ST_Equals(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Touches)).Should().Be("ST_Touches(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Within)).Should().Be("ST_Within(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Contains)).Should().Be("ST_Contains(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Overlaps)).Should().Be("ST_Overlaps(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Crosses)).Should().Be("ST_Crosses(t.shape, @g)");
            // PostGIS has the native within-distance function
            s.GeometryPredicate(Pr(SqlGeoPredicateId.DWithin, distance: 100)).Should().Be("ST_DWithin(t.shape, @g, 100)");
        }

        // ---- MySQL / MariaDB / SpatiaLite (OGC ST_*, portable within-distance) ----

        [Theory]
        [InlineData(typeof(MySql8LanguageSpecifics))]
        [InlineData(typeof(MariaDbLanguageSpecifics))]
        public void MysqlFamily_Functions_And_PortableDWithin(Type specificsType)
        {
            var s = (SqlDbLanguageSpecifics)Activator.CreateInstance(specificsType);
            s.GeometryFunction(Fn(SqlGeoFunctionId.FromWkb, a: null, parameter: P, srid: 4326)).Should().Be("ST_GeomFromWKB(@p, 4326)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.AsBinary)).Should().Be("ST_AsBinary(t.shape)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Distance, b: B)).Should().Be("ST_Distance(t.shape, @g)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Intersects)).Should().Be("ST_Intersects(t.shape, @g)");
            // no native ST_DWithin -> portable distance comparison
            s.GeometryPredicate(Pr(SqlGeoPredicateId.DWithin, distance: 100)).Should().Be("(ST_Distance(t.shape, @g) <= 100)");
        }

        // ---- SQL Server (method-call UDT, bit -> = 1) ----

        [Fact]
        public void Mssql_Functions()
        {
            var s = new MssqlDbLanguageSpecifics();
            s.GeometryFunction(Fn(SqlGeoFunctionId.FromWkb, a: null, parameter: P, srid: 4326)).Should().Be("geometry::STGeomFromWKB(@p, 4326)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.AsBinary)).Should().Be("t.shape.STAsBinary()");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Distance, b: B)).Should().Be("t.shape.STDistance(@g)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Area)).Should().Be("t.shape.STArea()");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Length)).Should().Be("t.shape.STLength()");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Srid)).Should().Be("t.shape.STSrid");
            s.GeometryFunction(Fn(SqlGeoFunctionId.GeometryType)).Should().Be("t.shape.STGeometryType()");
            s.GeometryFunction(Fn(SqlGeoFunctionId.IsEmpty)).Should().Be("t.shape.STIsEmpty()");
            s.GeometryFunction(Fn(SqlGeoFunctionId.X)).Should().Be("t.shape.STX");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Y)).Should().Be("t.shape.STY");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Envelope)).Should().Be("t.shape.STEnvelope()");
        }

        [Fact]
        public void Mssql_Predicates_NormalizedToBit()
        {
            var s = new MssqlDbLanguageSpecifics();
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Intersects)).Should().Be("(t.shape.STIntersects(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Disjoint)).Should().Be("(t.shape.STDisjoint(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Equals)).Should().Be("(t.shape.STEquals(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Touches)).Should().Be("(t.shape.STTouches(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Within)).Should().Be("(t.shape.STWithin(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Contains)).Should().Be("(t.shape.STContains(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Overlaps)).Should().Be("(t.shape.STOverlaps(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Crosses)).Should().Be("(t.shape.STCrosses(@g) = 1)");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.DWithin, distance: 100)).Should().Be("(t.shape.STDistance(@g) <= 100)");
        }

        // ---- Oracle (SDO_* packages, RELATE mask <> FALSE, Crosses unsupported) ----

        [Fact]
        public void Oracle_Functions_WithDefaultAndExplicitTolerance()
        {
            var s = new OracleDbLanguageSpecifics();
            // The WKB converters are null-guarded: SDO_UTIL's Java procs NPE on a NULL argument, so a NULL
            // geometry must render as NULL rather than call the proc (see GeometryNullRoundTrip*Test).
            s.GeometryFunction(Fn(SqlGeoFunctionId.FromWkb, a: null, parameter: P, srid: 4326)).Should().Be("CASE WHEN @p IS NULL THEN NULL ELSE SDO_UTIL.FROM_WKBGEOMETRY(@p) END");
            s.GeometryFunction(Fn(SqlGeoFunctionId.AsBinary)).Should().Be("CASE WHEN t.shape IS NULL THEN NULL ELSE SDO_UTIL.TO_WKBGEOMETRY(t.shape) END");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Distance, b: B)).Should().Be("SDO_GEOM.SDO_DISTANCE(t.shape, @g, 0.005)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Distance, b: B, tolerance: 0.5)).Should().Be("SDO_GEOM.SDO_DISTANCE(t.shape, @g, 0.5)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Area)).Should().Be("SDO_GEOM.SDO_AREA(t.shape, 0.005)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Length)).Should().Be("SDO_GEOM.SDO_LENGTH(t.shape, 0.005)");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Srid)).Should().Be("t.shape.SDO_SRID");
            s.GeometryFunction(Fn(SqlGeoFunctionId.GeometryType)).Should().Be("t.shape.GET_GTYPE()");
            s.GeometryFunction(Fn(SqlGeoFunctionId.X)).Should().Be("t.shape.SDO_POINT.X");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Y)).Should().Be("t.shape.SDO_POINT.Y");
            s.GeometryFunction(Fn(SqlGeoFunctionId.Envelope)).Should().Be("SDO_GEOM.SDO_MBR(t.shape)");
        }

        [Fact]
        public void Oracle_IsEmpty_IsUnsupported()
        {
            var s = new OracleDbLanguageSpecifics();
            ((Action)(() => s.GeometryFunction(Fn(SqlGeoFunctionId.IsEmpty))))
                .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }

        [Fact]
        public void Oracle_Predicates_RelateMasks()
        {
            var s = new OracleDbLanguageSpecifics();
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Intersects)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'ANYINTERACT', @g, 0.005) <> 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Disjoint)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'ANYINTERACT', @g, 0.005) = 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Equals)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'EQUAL', @g, 0.005) <> 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Touches)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'TOUCH', @g, 0.005) <> 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Within)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'INSIDE+COVEREDBY', @g, 0.005) <> 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Contains)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'CONTAINS+COVERS', @g, 0.005) <> 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.Overlaps)).Should().Be("(SDO_GEOM.RELATE(t.shape, 'OVERLAPBDYINTERSECT', @g, 0.005) <> 'FALSE')");
            s.GeometryPredicate(Pr(SqlGeoPredicateId.DWithin, distance: 100)).Should().Be("(SDO_GEOM.SDO_DISTANCE(t.shape, @g, 0.005) <= 100)");
        }

        [Fact]
        public void Oracle_Crosses_IsUnsupported()
        {
            var s = new OracleDbLanguageSpecifics();
            ((Action)(() => s.GeometryPredicate(Pr(SqlGeoPredicateId.Crosses))))
                .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }
    }
}

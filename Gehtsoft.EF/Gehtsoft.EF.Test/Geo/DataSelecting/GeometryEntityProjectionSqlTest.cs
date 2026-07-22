using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free tests of the entity-level geometry SELECT surface: scalar projection, order-by and
    /// group-by (rendered byte-identically so GROUP BY matches), the two whole-value forms
    /// (<c>Wkb</c>=<c>ST_AsBinary</c> vs <c>Native</c>=raw column), and the whole-entity auto-read wrapping a
    /// geometry column in the WKB output function while a plain column stays a bare column. Tuple/scalar
    /// queries start from <c>GetSelectEntitiesQueryBase</c> (empty resultset); the whole-entity read uses
    /// <c>GetSelectEntitiesQuery</c> (auto-selects every column). The methods resolve the column through
    /// <c>GetReference</c> and delegate to the pure-SQL SelectQueryBuilder. The dummy dialect renders the
    /// portable OGC grammar; the generated SQL is asserted by substring (the test grammar has no
    /// spatial-function rule).
    /// </summary>
    public class GeometryEntityProjectionSqlTest
    {
        [Entity(Scope = "geo_ent_proj_sql", Table = "geo_ent_proj_sql")]
        public class GeoEntProjSql
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name")]
            public string Name { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry, Srid = 4326)]
            public byte[] Shape { get; set; }
        }

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        private static string Sql(SelectEntitiesQueryBase q)
        {
            q.Builder.PrepareQuery();
            return q.Builder.Query;
        }

        [Fact]
        public void Projection_ScalarMeasurement()
        {
            using var connection = GeoConnection();
            using var q = connection.GetSelectEntitiesQueryBase<GeoEntProjSql>();
            q.AddGeometryScalarToResultset<GeoEntProjSql>(SqlGeoFunctionId.Area, "Shape", DbType.Double, "area");
            string sql = Sql(q);

            sql.Should().Contain("ST_Area(");
            sql.Should().Contain(".shape) AS area");
        }

        [Fact]
        public void GroupBy_GeoScalar()
        {
            using var connection = GeoConnection();
            using var q = connection.GetSelectEntitiesQueryBase<GeoEntProjSql>();
            q.AddGeometryScalarToResultset<GeoEntProjSql>(SqlGeoFunctionId.Area, "Shape", DbType.Double, "area");
            q.AddToResultset(AggFn.Count, "ID", "cnt");
            q.AddGeometryScalarToGroupBy<GeoEntProjSql>(SqlGeoFunctionId.Area, "Shape");
            string sql = Sql(q);

            sql.Should().Contain("ST_Area(");
            sql.Should().Contain("GROUP BY ST_Area(");
        }

        [Fact]
        public void OrderBy_Distance_NearestNeighbour()
        {
            using var connection = GeoConnection();
            using var q = connection.GetSelectEntitiesQueryBase<GeoEntProjSql>();
            q.AddToResultset("ID");
            q.AddGeometryScalarToOrderBy<GeoEntProjSql>(SqlGeoFunctionId.Distance, "Shape", SortDir.Asc, parameterName: "p");
            string sql = Sql(q);

            sql.Should().Contain("ORDER BY ST_Distance(");
            sql.Should().Contain("ST_GeomFromWKB(@p, 4326)");
        }

        [Fact]
        public void NativeForm_EmitsRawColumn_NoOutputWrap()
        {
            using var connection = GeoConnection();
            using var q = connection.GetSelectEntitiesQueryBase<GeoEntProjSql>();
            q.AddGeometryToResultset<GeoEntProjSql>("Shape", GeometryValueForm.Native, "g");
            string sql = Sql(q);

            sql.Should().Contain(".shape AS g");
            sql.Should().NotContain("ST_AsBinary(");
        }

        [Fact]
        public void WholeEntityRead_WrapsGeometryColumn_LeavesPlainColumnBare()
        {
            using var connection = GeoConnection();
            // GetSelectEntitiesQuery auto-selects every column of the entity.
            using var q = connection.GetSelectEntitiesQuery<GeoEntProjSql>();
            string sql = Sql(q);

            // the geometry column is projected through the WKB output wrap; the plain column stays a bare column.
            sql.Should().Contain("ST_AsBinary(");
            sql.Should().Contain(".shape)");
            sql.Should().MatchRegex(@"\.name\b");
            // exactly one ST_AsBinary - only the geometry column is wrapped, the plain column is not
            System.Text.RegularExpressions.Regex.Matches(sql, @"ST_AsBinary\(").Count.Should().Be(1);
        }
    }
}

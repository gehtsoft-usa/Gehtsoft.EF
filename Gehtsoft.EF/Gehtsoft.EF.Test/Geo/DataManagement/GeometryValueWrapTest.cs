using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// Deep, DB-free tests of the geometry value-wrap on the INSERT and UPDATE builders: the WKB
    /// parameter is wrapped in the dialect's constructor function in the generated SQL. The dummy dialect
    /// renders the portable OGC grammar (per-driver renderings are covered by GeometryRenderTest); this
    /// asserts the driver-agnostic builder wiring emits the wrapped expression exactly. (The test SQL
    /// grammar has no generic function-call / function-valued INSERT rule, so — as with the geometry
    /// DDL-generation tests — the generated statement is asserted by exact string.)
    /// </summary>
    public class GeometryValueWrapTest
    {
        private static TableDescriptor GeoTable()
            => new TableDescriptor("geo_rt", new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, false, false, true, new SpatialIndexDefinition[0]),
                },
            });

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        [Fact]
        public void Insert_WrapsWkbParameterInConstructor()
        {
            using var connection = GeoConnection();
            var specifics = connection.GetLanguageSpecifics();
            var table = GeoTable();

            var insert = connection.GetInsertQueryBuilder(table);
            insert.SetColumnValueExpressions(("shape", specifics.GeometryFunction(
                new GeoFunctionRequest(SqlGeoFunctionId.FromWkb, parameter: InsertQueryBuilder.ParameterToken, srid: 4326))));
            insert.PrepareQuery();

            // the autoincrement id is omitted; the geometry column's value is the constructor-wrapped parameter
            insert.Query.Should().Be("INSERT INTO geo_rt ( shape) VALUES (ST_GeomFromWKB(@shape, 4326) ) ");
        }

        [Fact]
        public void Insert_NonGeometryColumn_StillUsesPlainParameter()
        {
            using var connection = GeoConnection();
            var table = GeoTable();

            // no value expression set -> the plain bound parameter is emitted
            var insert = connection.GetInsertQueryBuilder(table);
            insert.IncludeOnly("shape");
            insert.PrepareQuery();
            insert.Query.Should().Be("INSERT INTO geo_rt ( shape) VALUES (@shape ) ");
        }

        [Fact]
        public void Update_WrapsWkbParameterInConstructor()
        {
            using var connection = GeoConnection();
            var specifics = connection.GetLanguageSpecifics();
            var table = GeoTable();

            var update = connection.GetUpdateQueryBuilder(table);
            update.AddUpdateColumnExpression(table["shape"], specifics.GeometryFunction(
                new GeoFunctionRequest(SqlGeoFunctionId.FromWkb, parameter: "@shape", srid: 4326)));
            update.PrepareQuery();

            update.Query.Should().Be("UPDATE geo_rt SET shape=ST_GeomFromWKB(@shape, 4326)");
        }
    }
}

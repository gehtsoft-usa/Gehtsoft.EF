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
    /// Deep, DB-free tests of the geometry value-wrap on the INSERT and UPDATE builders. A geometry column
    /// stores WKB but the engine's column type is the native spatial type, so the builders detect the
    /// column's <see cref="TableDescriptor.ColumnInfo.Geometry"/> metadata and wrap the bound WKB parameter
    /// in the dialect's constructor function automatically - the metadata-driven auto-wrap (mirrors the
    /// autoincrement metadata emission in the same INSERT loop). An explicit value expression
    /// (<see cref="InsertQueryBuilder.SetColumnValueExpressions"/> /
    /// <see cref="UpdateQueryBuilder.AddUpdateColumnExpression"/>) still overrides it, and non-geometry
    /// columns stay plain parameters. The dummy dialect renders the portable OGC grammar (per-driver
    /// renderings are covered by GeometryRenderTest); this asserts the driver-agnostic builder wiring emits
    /// the wrapped expression exactly. (The test SQL grammar has no generic function-call / function-valued
    /// INSERT rule, so - as with the geometry DDL-generation tests - the generated statement is asserted by
    /// exact string.)
    /// </summary>
    public class GeometryValueWrapTest
    {
        private static TableDescriptor GeoTable()
            => new TableDescriptor("geo_rt", new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true },
                new TableDescriptor.ColumnInfo { Name = "name", DbType = DbType.String, Size = 64 },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, false, false, true, System.Array.Empty<SpatialIndexDefinition>()),
                },
            });

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        [Fact]
        public void Insert_AutoWrapsGeometryColumn()
        {
            using var connection = GeoConnection();
            var table = GeoTable();

            // no explicit value expression: the geometry metadata drives the FromWkb constructor wrap,
            // with the SRID taken from the column metadata.
            var insert = connection.GetInsertQueryBuilder(table);
            insert.IncludeOnly("shape");
            insert.PrepareQuery();
            insert.Query.Should().Be("INSERT INTO geo_rt ( shape) VALUES (ST_GeomFromWKB(@shape, 4326) ) ");
        }

        [Fact]
        public void Insert_ExplicitExpressionOverridesAutoWrap()
        {
            using var connection = GeoConnection();
            var specifics = connection.GetLanguageSpecifics();
            var table = GeoTable();

            // an explicit expression wins over the auto-wrap - a distinct SRID proves it is the explicit
            // expression, not the metadata-driven one, that rendered.
            var insert = connection.GetInsertQueryBuilder(table);
            insert.IncludeOnly("shape");
            insert.SetColumnValueExpressions(("shape", specifics.GeometryFunction(
                new GeoFunctionRequest(SqlGeoFunctionId.FromWkb, parameter: InsertQueryBuilder.ParameterToken, srid: 9999))));
            insert.PrepareQuery();
            insert.Query.Should().Be("INSERT INTO geo_rt ( shape) VALUES (ST_GeomFromWKB(@shape, 9999) ) ");
        }

        [Fact]
        public void Insert_NonGeometryColumn_UsesPlainParameter()
        {
            using var connection = GeoConnection();
            var table = GeoTable();

            var insert = connection.GetInsertQueryBuilder(table);
            insert.IncludeOnly("name");
            insert.PrepareQuery();
            insert.Query.Should().Be("INSERT INTO geo_rt ( name) VALUES (@name ) ");
        }

        [Fact]
        public void Update_AutoWrapsGeometryColumn()
        {
            using var connection = GeoConnection();
            var table = GeoTable();

            var update = connection.GetUpdateQueryBuilder(table);
            update.AddUpdateColumn(table["shape"]);
            update.PrepareQuery();
            update.Query.Should().Be("UPDATE geo_rt SET shape=ST_GeomFromWKB(@shape, 4326)");
        }

        [Fact]
        public void Update_ExplicitExpressionOverridesAutoWrap()
        {
            using var connection = GeoConnection();
            var specifics = connection.GetLanguageSpecifics();
            var table = GeoTable();

            var update = connection.GetUpdateQueryBuilder(table);
            update.AddUpdateColumnExpression(table["shape"], specifics.GeometryFunction(
                new GeoFunctionRequest(SqlGeoFunctionId.FromWkb, parameter: "@shape", srid: 9999)));
            update.PrepareQuery();
            update.Query.Should().Be("UPDATE geo_rt SET shape=ST_GeomFromWKB(@shape, 9999)");
        }

        [Fact]
        public void Update_NonGeometryColumn_UsesPlainParameter()
        {
            using var connection = GeoConnection();
            var table = GeoTable();

            var update = connection.GetUpdateQueryBuilder(table);
            update.AddUpdateColumn(table["name"]);
            update.PrepareQuery();
            update.Query.Should().Be("UPDATE geo_rt SET name=@name");
        }
    }
}

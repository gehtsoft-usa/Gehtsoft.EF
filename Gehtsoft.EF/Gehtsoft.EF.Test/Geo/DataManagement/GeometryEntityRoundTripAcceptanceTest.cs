using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using NetTopologySuite.Geometries;
using Xunit;
using NtsGeom = NetTopologySuite.Geometries.Geometry;

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// Acceptance-tier round-trip of geometry <b>entity</b> properties through the entity insert/update query
    /// path against every configured live server engine (MSSQL, Oracle, PostGIS, MariaDB, MySQL 8). Proves
    /// that <c>GetInsertEntityQuery</c> / <c>GetUpdateEntityQuery</c> round-trip a geometry property purely by
    /// inheriting the Increment-1 auto-wrap (entity builders zero-touch), for both a raw <c>byte[]</c> (WKB)
    /// property and an NTS object property (whose decorating <c>GeometryPropertyAccessor</c> serializes the
    /// object to WKB before the wrap), and that a nullable object property set to <c>null</c> round-trips as
    /// <c>null</c>. Read-back uses the Phase-4 pure-SQL select output-wrap because entity SELECT of a geometry
    /// is Area 3 (a later increment). The geometry is 2-D (MySQL is 2-D only), the columns are non-indexed.
    /// Serialized on the codec-registration collection because the object accessor resolves the global codec.
    /// SQLite/SpatiaLite is covered by its own dedicated test.
    /// </summary>
    [Collection("GeometryCodecRegistration")]
    public class GeometryEntityRoundTripAcceptanceTest : IClassFixture<GeometryEntityRoundTripAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryEntityRoundTripAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryEntityRoundTripAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geoent_acc", Table = "geo_ent_acc")]
        public class GeoEntAcc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape_a", Subtype = GeometrySubtype.Point)]
            public byte[] Raw { get; set; }

            [GeometryEntityProperty(Field = "shape_b", Subtype = GeometrySubtype.Point, Nullable = true)]
            public NtsGeom Loc { get; set; }
        }

        private static void DropTable(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("geo_ent_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoEntAcc>())
                    q.Execute();
        }

        private static void SkipIfGeometryUnsupported(SqlDbConnection connection, string connectionName)
        {
            string driver = AppConfiguration.Instance.GetSqlConnection(connectionName).Driver;
            if (driver == "npgsql")
            {
                using var q = connection.GetQuery(
                    "SELECT COALESCE((SELECT 'yes' FROM pg_type WHERE typname = 'geometry' LIMIT 1), 'no')", true);
                q.ExecuteReader();
                string present = q.ReadNext() ? q.GetValue<object>(0)?.ToString() : null;
                if (present != "yes")
                    Assert.Skip("PostGIS is not installed in this database (run 'CREATE EXTENSION postgis;').");
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void EntityInsertUpdate_RoundTripsGeometryProperties(string connectionName)
        {
            NtsGeometry.Register();
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoEntAcc), "geoent_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoEntAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo raw = GeometryRoundTripSupport.ColumnByName(table, "shape_a");
                TableDescriptor.ColumnInfo loc = GeometryRoundTripSupport.ColumnByName(table, "shape_b");

                // INSERT through the entity path: byte[] property auto-wrapped directly; object property
                // serialized to WKB by the accessor then auto-wrapped.
                var entity = new GeoEntAcc
                {
                    Raw = GeometryRoundTripSupport.ToWkb(new Point(1.5, 2.5) { SRID = 4326 }),
                    Loc = new Point(3.5, 4.5) { SRID = 4326 },
                };
                using (var q = connection.GetInsertEntityQuery<GeoEntAcc>())
                    q.Execute(entity);

                ((Point)GeometryRoundTripSupport.SelectShape(connection, table, raw)).Should().Match<Point>(p => p.X == 1.5 && p.Y == 2.5);
                ((Point)GeometryRoundTripSupport.SelectShape(connection, table, loc)).Should().Match<Point>(p => p.X == 3.5 && p.Y == 4.5);

                // UPDATE (by id): change the raw value, null the object property.
                entity.Raw = GeometryRoundTripSupport.ToWkb(new Point(-71.0, 42.0) { SRID = 4326 });
                entity.Loc = null;
                using (var q = connection.GetUpdateEntityQuery<GeoEntAcc>())
                    q.Execute(entity);

                ((Point)GeometryRoundTripSupport.SelectShape(connection, table, raw)).Should().Match<Point>(p => p.X == -71.0 && p.Y == 42.0);
                GeometryRoundTripSupport.SelectShape(connection, table, loc).Should().BeNull();
            }
            finally
            {
                DropTable(connection);
            }
        }
    }
}

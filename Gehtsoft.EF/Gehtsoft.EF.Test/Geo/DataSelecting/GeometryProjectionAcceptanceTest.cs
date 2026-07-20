using System.Reflection;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Acceptance-tier geometry scalar projection / order-by-distance / group-by / aggregation against
    /// every live server engine (MSSQL, Oracle, PostGIS, MariaDB, MySQL 8), asserting the actual computed
    /// values. Generic geometry, SRID 0 (Cartesian) so all engines compute identical planar values.
    /// SQLite/SpatiaLite has its own dedicated test.
    /// </summary>
    public class GeometryProjectionAcceptanceTest : IClassFixture<GeometryProjectionAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryProjectionAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryProjectionAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geoproj_acc", Table = "geo_proj_acc")]
        public class GeoProjAcc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry, Srid = 0)]
            public byte[] Shape { get; set; }
        }

        private static void DropTable(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("geo_proj_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoProjAcc>())
                    q.Execute();
        }

        private static void SkipIfGeometryUnsupported(SqlDbConnection connection, string connectionName)
        {
            if (AppConfiguration.Instance.GetSqlConnection(connectionName).Driver == "npgsql")
            {
                using var q = connection.GetQuery(
                    "SELECT COALESCE((SELECT 'yes' FROM pg_type WHERE typname = 'geometry' LIMIT 1), 'no')", true);
                q.ExecuteReader();
                if ((q.ReadNext() ? q.GetValue<object>(0)?.ToString() : null) != "yes")
                    Assert.Skip("PostGIS is not installed in this database (run 'CREATE EXTENSION postgis;').");
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void Projection_OrderBy_GroupBy_Aggregation(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoProjAcc), "geoproj_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoProjAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                GeometryProjectionChecks.RunAll(connection, table, shape);
            }
            finally
            {
                DropTable(connection);
            }
        }
    }
}

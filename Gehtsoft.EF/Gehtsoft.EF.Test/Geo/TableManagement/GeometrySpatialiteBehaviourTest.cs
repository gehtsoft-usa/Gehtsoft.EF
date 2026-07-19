using System;
using System.IO;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.TableManagement
{
    /// <summary>
    /// Behavioural create of a geometry table on a live SQLite + SpatiaLite database via the driver's
    /// enable-spatial path (which promotes the e_sqlite3 symbols so mod_spatialite loads, then
    /// AddGeometryColumn / CreateSpatialIndex). The native library comes from the Spatialite.Native
    /// package; the test skips when it is unavailable. Serialized so it does not race other SQLite
    /// tests through the global enable-spatial flag.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometrySpatialiteBehaviourTest
    {
        [Entity(Scope = "geospatialite", Table = "geo_sl")]
        public class GeoSl
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            [SpatialIndex]
            public byte[] Shape { get; set; }
        }

        // Same table, no geometry — used to create the base table so the ALTER path adds the geo column.
        [Entity(Scope = "geospatialite_alter", Table = "geo_alter")]
        public class GeoAlterBase
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        // Same table plus the geometry column; its descriptor is the source of the column to ALTER-add.
        [Entity(Scope = "geospatialite_alter", Table = "geo_alter")]
        public class GeoAlter
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            [SpatialIndex]
            public byte[] Shape { get; set; }
        }

        private static string LocateLibrary()
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                      : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "linux";
            string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64"
                        : RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "x86" : "x64";
            string file = os == "win" ? "mod_spatialite.dll" : os == "osx" ? "mod_spatialite.dylib" : "mod_spatialite.so";
            string path = Path.Combine(AppContext.BaseDirectory, "runtimes", $"{os}-{arch}", "native", file);
            return File.Exists(path) ? path : null;
        }

        [Fact]
        public void CreateGeometryTable_AddsColumnAndSpatialIndex()
        {
            string library = LocateLibrary();
            if (library == null)
                Assert.Skip("The SpatiaLite native library (Spatialite.Native) is not available for this OS/architecture.");

            bool previousEnabled = SqliteGlobalOptions.EnableSpatial;
            string previousLibrary = SqliteGlobalOptions.SpatialiteLibrary;
            SqliteGlobalOptions.SpatialiteLibrary = library;
            SqliteGlobalOptions.EnableSpatial = true;
            try
            {
                using SqliteDbConnection connection = (SqliteDbConnection)SqliteDbConnectionFactory.CreateMemory();

                using (var q = connection.GetCreateEntityQuery<GeoSl>())
                    q.Execute();

                connection.DoesObjectExist("geo_sl", null, "table").Should().BeTrue("the table is created");
                connection.DoesObjectExist("geo_sl", "shape", "column").Should().BeTrue("AddGeometryColumn created the geometry column");
                // SpatiaLite CreateSpatialIndex builds an R-tree virtual table named idx_<table>_<column>.
                connection.DoesObjectExist("idx_geo_sl_shape", null, "table").Should().BeTrue("the spatial index (R-tree) is created");
            }
            finally
            {
                SqliteGlobalOptions.EnableSpatial = previousEnabled;
                SqliteGlobalOptions.SpatialiteLibrary = previousLibrary;
            }
        }

        [Fact]
        public void AlterTable_AddsGeometryColumnAndSpatialIndex()
        {
            string library = LocateLibrary();
            if (library == null)
                Assert.Skip("The SpatiaLite native library (Spatialite.Native) is not available for this OS/architecture.");

            bool previousEnabled = SqliteGlobalOptions.EnableSpatial;
            string previousLibrary = SqliteGlobalOptions.SpatialiteLibrary;
            SqliteGlobalOptions.SpatialiteLibrary = library;
            SqliteGlobalOptions.EnableSpatial = true;
            try
            {
                using SqliteDbConnection connection = (SqliteDbConnection)SqliteDbConnectionFactory.CreateMemory();

                // create the base table without the geometry column
                using (var q = connection.GetCreateEntityQuery<GeoAlterBase>())
                    q.Execute();
                connection.DoesObjectExist("geo_alter", "shape", "column").Should().BeFalse("the base table has no geometry column yet");

                // the geometry column (with its spatial index) comes from the entity descriptor, not hand-built metadata
                TableDescriptor descriptor = AllEntities.Inst[typeof(GeoAlter)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = null;
                foreach (TableDescriptor.ColumnInfo column in descriptor)
                    if (column.Name == "shape")
                        shape = column;
                shape.Should().NotBeNull();

                // drive the ALTER through the same builder + execution path the catalogue uses (AddColumns)
                var builder = connection.GetAlterTableQueryBuilder();
                builder.SetTable(descriptor, new[] { shape }, null);
                foreach (var queryText in builder.GetQueries())
                    // SpatiaLite emits its geometry DDL as SELECT AddGeometryColumn(...) / CreateSpatialIndex(...),
                    // which trips the scalar-query guard; suppress it (as the catalogue's AddColumns path must too).
                    using (var query = connection.GetQuery(queryText, true))
                        query.ExecuteNoData();

                connection.DoesObjectExist("geo_alter", "shape", "column").Should().BeTrue("AddGeometryColumn added the geometry column to the existing table");
                connection.DoesObjectExist("idx_geo_alter_shape", null, "table").Should().BeTrue("CreateSpatialIndex built the R-tree for the added column");
            }
            finally
            {
                SqliteGlobalOptions.EnableSpatial = previousEnabled;
                SqliteGlobalOptions.SpatialiteLibrary = previousLibrary;
            }
        }
    }
}

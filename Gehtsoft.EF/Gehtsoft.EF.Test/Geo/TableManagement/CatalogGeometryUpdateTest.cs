using System;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Catalog;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.TableManagement
{
    /// <summary>
    /// Geometry schema reconciliation through the full <see cref="CatalogEntityController"/> update path on a
    /// live SQLite + SpatiaLite database: add a geometry column, add / drop a spatial index on an existing
    /// geometry column, and the data-loss refusal when a geometry column would be dropped implicitly. The
    /// migration recipe mirrors the JSON/dynamic-properties tests — build the V1 shape, seed the target
    /// scope's catalogue with it, then run UpdateTables against the V2 model.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class CatalogGeometryUpdateTest
    {
        private static readonly Assembly Asm = typeof(CatalogGeometryUpdateTest).Assembly;

        // --- add geometry column: V1 has no geometry, V2 gains one ---
        [Entity(Scope = "geoupd_v1", Table = "geo_upd")]
        public class GeoUpdV1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        [Entity(Scope = "geoupd_v2", Table = "geo_upd")]
        public class GeoUpdV2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            [SpatialIndex]
            public byte[] Shape { get; set; }
        }

        // --- spatial-index reconcile on an unchanged geometry column ---
        [Entity(Scope = "georidx_v1", Table = "geo_ridx")]
        public class GeoRIdxV1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        [Entity(Scope = "georidx_v2", Table = "geo_ridx")]
        public class GeoRIdxV2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            [SpatialIndex]
            public byte[] Shape { get; set; }
        }

        // --- implicit geometry-column drop (no [ObsoleteEntityProperty]) ---
        [Entity(Scope = "geodrop_v1", Table = "geo_drop")]
        public class GeoDropV1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            [SpatialIndex]
            public byte[] Shape { get; set; }
        }

        [Entity(Scope = "geodrop_v2", Table = "geo_drop")]
        public class GeoDropV2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        [Fact]
        public void UpdateTables_AddsGeometryColumnAndSpatialIndex()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                CatalogTestSupport.ResetCatalog(connection, Asm);

                // before: table with no geometry column
                new CatalogEntityController(typeof(GeoUpdV1), "geoupd_v1").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("geo_upd", "shape", "column").Should().BeFalse("no geometry column yet");

                // migrate to the model that declares the geometry column + spatial index
                CatalogTestSupport.Seed(connection, "geoupd_v2", "geo_upd", typeof(GeoUpdV1), "1.0.0");
                new CatalogEntityController(typeof(GeoUpdV2), "geoupd_v2").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

                connection.DoesObjectExist("geo_upd", "shape", "column").Should().BeTrue("the geometry column was added on update");
                connection.DoesObjectExist("idx_geo_upd_shape", null, "table").Should().BeTrue("its spatial index (R-tree) was created");
            });
        }

        [Fact]
        public void UpdateTables_AddsSpatialIndex_OnExistingGeometryColumn()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                CatalogTestSupport.ResetCatalog(connection, Asm);

                // before: geometry column, no spatial index
                new CatalogEntityController(typeof(GeoRIdxV1), "georidx_v1").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("idx_geo_ridx_shape", null, "table").Should().BeFalse("no spatial index declared yet");

                // migrate to the model that declares the spatial index -> it is created
                CatalogTestSupport.Seed(connection, "georidx_v2", "geo_ridx", typeof(GeoRIdxV1), "1.0.0");
                new CatalogEntityController(typeof(GeoRIdxV2), "georidx_v2").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

                connection.DoesObjectExist("idx_geo_ridx_shape", null, "table").Should().BeTrue("the spatial index was added on update");
            });
        }

        [Fact]
        public void UpdateTables_DropsSpatialIndex_OnExistingGeometryColumn()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                CatalogTestSupport.ResetCatalog(connection, Asm);

                // before: geometry column WITH a spatial index
                new CatalogEntityController(typeof(GeoRIdxV2), "georidx_v2").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("idx_geo_ridx_shape", null, "table").Should().BeTrue("the spatial index is present initially");

                // migrate to the model that no longer declares it -> the framework-owned index is dropped
                CatalogTestSupport.Seed(connection, "georidx_v1", "geo_ridx", typeof(GeoRIdxV2), "1.0.0");
                new CatalogEntityController(typeof(GeoRIdxV1), "georidx_v1").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

                connection.DoesObjectExist("idx_geo_ridx_shape", null, "table").Should().BeFalse("the spatial index was dropped on update");
            });
        }

        [Fact]
        public void UpdateTables_ImplicitGeometryColumnDrop_IsRefused()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                CatalogTestSupport.ResetCatalog(connection, Asm);

                // before: table with a geometry column
                new CatalogEntityController(typeof(GeoDropV1), "geodrop_v1").CreateTables(connection, "1.0.0");

                // migrate to a model that dropped the property WITHOUT [ObsoleteEntityProperty] -> data-loss refusal
                CatalogTestSupport.Seed(connection, "geodrop_v2", "geo_drop", typeof(GeoDropV1), "1.0.0");
                Action act = () => new CatalogEntityController(typeof(GeoDropV2), "geodrop_v2")
                    .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

                act.Should().Throw<EfSqlException>()
                    .Which.ErrorCode.Should().Be(EfExceptionCode.CatalogColumnDropWouldLoseData);
            });
        }
    }
}

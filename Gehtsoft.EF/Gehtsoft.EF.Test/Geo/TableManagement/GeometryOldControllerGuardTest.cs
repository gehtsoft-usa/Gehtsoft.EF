using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.TableManagement
{
    /// <summary>
    /// Geometry schema management is CatalogEntityController-only; the obsolete introspection controller must
    /// refuse loudly the moment it meets a geometry field rather than silently mishandling it (it cannot
    /// reconcile spatial indexes or add/drop geometry columns portably).
    /// </summary>
    public class GeometryOldControllerGuardTest
    {
        [Entity(Scope = "geo_oldguard", Table = "geo_oldguard_t")]
        public class GeoEntity
        {
            [AutoId]
            public int Id { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Nullable = true)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void OldController_RefusesGeometry_OnCreate()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            Action act = () => new CreateEntityControllerInternal(typeof(GeoEntity), "geo_oldguard")
                .CreateTables(connection);
            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.GeometryRequiresCatalogController);
        }

        [Fact]
        public void OldController_RefusesGeometry_OnUpdate()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            Action act = () => new CreateEntityControllerInternal(typeof(GeoEntity), "geo_oldguard")
                .UpdateTables(connection, EntityUpdateMode.Update);
            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.GeometryRequiresCatalogController);
        }
    }
}

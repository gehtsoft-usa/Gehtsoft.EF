using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Unit tests for the Phase-5 resolution seam (prereq P-A): <see cref="IEntityInfoProvider"/> gains a
    /// <c>TryResolveColumn</c> that returns a property's full <see cref="TableDescriptor.ColumnInfo"/> and the
    /// <see cref="QueryBuilderEntity"/> it belongs to, so the geometry WHERE/SELECT methods can read
    /// <c>column.Geometry.Srid</c> - metadata the alias-only <c>Alias</c> path (used by JSON) cannot carry.
    /// The resolver is surfaced from the existing item index on <c>EntityQueryWithWhereBuilder</c>; it resolves
    /// both by path and by (type, occurrence, property), works for non-geometry columns too (with a null
    /// <c>Geometry</c>), and reports a miss with <c>false</c> rather than throwing (unlike <c>Alias</c>).
    /// </summary>
    public class GeometryEntityColumnResolutionTest
    {
        [Entity(Scope = "georesolve", Table = "geo_resolve")]
        public class GeoResolve
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name")]
            public string Name { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Srid = 3857)]
            public byte[] Shape { get; set; }
        }

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        private static IEntityInfoProvider Provider(DummySqlConnection connection)
        {
            var query = connection.GetMultiDeleteEntityQuery<GeoResolve>();
            return (EntityQueryWithWhereBuilder)query.EntityQueryBuilder;
        }

        [Fact]
        public void ResolvesGeometryColumn_ByPath_CarriesGeometryMetadataAndEntity()
        {
            using var connection = GeoConnection();
            var provider = Provider(connection);

            provider.TryResolveColumn("Shape", out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity)
                .Should().BeTrue();
            column.Should().NotBeNull();
            column.Name.Should().Be("shape");
            column.Geometry.Should().NotBeNull();
            column.Geometry.Srid.Should().Be(3857);
            entity.Should().NotBeNull();
        }

        [Fact]
        public void ResolvesGeometryColumn_ByTypeAndName_SameColumn()
        {
            using var connection = GeoConnection();
            var provider = Provider(connection);

            provider.TryResolveColumn("Shape", out TableDescriptor.ColumnInfo byPath, out _).Should().BeTrue();
            provider.TryResolveColumn(typeof(GeoResolve), 0, "Shape", out TableDescriptor.ColumnInfo byType, out QueryBuilderEntity entity)
                .Should().BeTrue();
            byType.Should().BeSameAs(byPath);
            entity.Should().NotBeNull();
        }

        [Fact]
        public void ResolvesNonGeometryColumn_WithNullGeometry()
        {
            using var connection = GeoConnection();
            var provider = Provider(connection);

            provider.TryResolveColumn("Name", out TableDescriptor.ColumnInfo column, out _).Should().BeTrue();
            column.Should().NotBeNull();
            column.Geometry.Should().BeNull();
        }

        [Fact]
        public void UnknownProperty_ReturnsFalse_WithoutThrowing()
        {
            using var connection = GeoConnection();
            var provider = Provider(connection);

            provider.TryResolveColumn("Nonexistent", out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity)
                .Should().BeFalse();
            column.Should().BeNull();
            entity.Should().BeNull();
        }
    }
}

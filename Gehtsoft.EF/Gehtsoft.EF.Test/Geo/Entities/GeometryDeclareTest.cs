using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.Entities
{
    /// <summary>Declare tests for the byte[] (WKB) geometry-property path — no codec involved.</summary>
    public class GeometryDeclareTest
    {
        [Entity(Scope = "geodeclare", Table = "geo_bytes")]
        public class GeoBytesOwner
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Srid = 3857, Subtype = GeometrySubtype.Polygon, HasZ = true, HasM = true, Nullable = true)]
            [SpatialIndex(-180, -90, 180, 90)]
            public byte[] Shape { get; set; }

            [GeometryEntityProperty(Field = "track")]
            [SpatialIndex(Name = "track_ix", Tolerance = 0.01)]
            public byte[] Track { get; set; }
        }

        private static TableDescriptor.ColumnInfo Column(TableDescriptor td, string id)
        {
            foreach (TableDescriptor.ColumnInfo c in td)
                if (c.ID == id)
                    return c;
            return null;
        }

        [Fact]
        public void ByteArrayGeometry_IsRecognized_WithDescriptorAndNoDecoration()
        {
            TableDescriptor td = AllEntities.Inst[typeof(GeoBytesOwner)].TableDescriptor;
            var shape = Column(td, "Shape");

            shape.Should().NotBeNull();
            shape.Name.Should().Be("shape");
            shape.DbType.Should().Be(DbType.Binary);
            shape.Nullable.Should().BeTrue();
            shape.Geometry.Should().NotBeNull();
            shape.Geometry.ClrType.Should().Be(typeof(byte[]));
            shape.Geometry.Srid.Should().Be(3857);
            shape.Geometry.Subtype.Should().Be(GeometrySubtype.Polygon);
            shape.Geometry.HasZ.Should().BeTrue();
            shape.Geometry.HasM.Should().BeTrue();

            // a byte[] geometry property keeps its ordinary accessor (no codec wrapper).
            shape.PropertyAccessor.Should().NotBeOfType<GeometryPropertyAccessor>();
        }

        [Fact]
        public void Defaults_Srid4326_SubtypeAny_NoZM()
        {
            TableDescriptor td = AllEntities.Inst[typeof(GeoBytesOwner)].TableDescriptor;
            var track = Column(td, "Track");

            track.Geometry.Srid.Should().Be(4326, "SRID defaults to EPSG:4326");
            track.Geometry.Subtype.Should().Be(GeometrySubtype.Geometry, "subtype defaults to any");
            track.Geometry.HasZ.Should().BeFalse();
            track.Geometry.HasM.Should().BeFalse();
            track.Geometry.Nullable.Should().BeFalse();
        }

        [Fact]
        public void SpatialIndex_WithBoundingBox_DerivedName()
        {
            TableDescriptor td = AllEntities.Inst[typeof(GeoBytesOwner)].TableDescriptor;
            var indexes = Column(td, "Shape").Geometry.Indexes;

            indexes.Should().HaveCount(1);
            var ix = indexes[0];
            ix.Name.Should().Be("shape_sidx", "the name is derived from the column when not declared");
            ix.HasBoundingBox.Should().BeTrue();
            ix.MinX.Should().Be(-180);
            ix.MinY.Should().Be(-90);
            ix.MaxX.Should().Be(180);
            ix.MaxY.Should().Be(90);
            ix.Tolerance.Should().Be(SpatialIndexAttribute.DefaultTolerance);
        }

        [Fact]
        public void SpatialIndex_NamedWithoutBoundingBox_KeepsNameAndTolerance()
        {
            TableDescriptor td = AllEntities.Inst[typeof(GeoBytesOwner)].TableDescriptor;
            var indexes = Column(td, "Track").Geometry.Indexes;

            indexes.Should().HaveCount(1);
            var ix = indexes[0];
            ix.Name.Should().Be("track_ix");
            ix.HasBoundingBox.Should().BeFalse();
            ix.Tolerance.Should().Be(0.01);
        }

        [Fact]
        public void RegularColumns_HaveNoGeometryMetadata()
        {
            TableDescriptor td = AllEntities.Inst[typeof(GeoBytesOwner)].TableDescriptor;
            Column(td, "ID").Geometry.Should().BeNull();
        }
    }
}

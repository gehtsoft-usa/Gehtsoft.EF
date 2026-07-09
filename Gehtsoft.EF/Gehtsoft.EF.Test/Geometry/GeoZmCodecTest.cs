using AwesomeAssertions;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geometry
{
    public class GeoZmCodecTest
    {
        [Theory]
        [InlineData("POINT Z (1 2 3)")]
        [InlineData("POINT M (1 2 3)")]
        [InlineData("POINT ZM (1 2 3 4)")]
        [InlineData("LINESTRING Z (1 2 3, 4 5 6)")]
        [InlineData("LINESTRING ZM (1 2 3 4, 5 6 7 8)")]
        [InlineData("POLYGON Z ((0 0 1, 0 1 1, 1 1 1, 1 0 1, 0 0 1))")]
        [InlineData("MULTIPOINT Z ((1 2 3), (4 5 6))")]
        [InlineData("MULTILINESTRING M ((1 2 3, 4 5 6))")]
        public void WktRoundTrip_TaggedDimensions_AreCanonical(string wkt)
        {
            GeoGeometry geometry = GeoGeometry.Parse(wkt);
            geometry.ToWkt(includeSrid: false).Should().Be(wkt);
        }

        [Theory]
        [InlineData("POINT Z (1 2 3)")]
        [InlineData("POINT M (1 2 3)")]
        [InlineData("POINT ZM (1 2 3 4)")]
        [InlineData("LINESTRING ZM (1 2 3 4, 5 6 7 8)")]
        [InlineData("MULTIPOLYGON ZM (((0 0 1 5, 0 1 1 6, 1 1 1 7, 0 0 1 5)))")]
        public void WkbRoundTrip_TaggedDimensions_PreserveGeometry(string wkt)
        {
            GeoGeometry original = GeoGeometry.Parse(wkt);
            GeoGeometry restored = GeoGeometry.FromWkb(original.ToWkb(), original.Srid);
            restored.Should().Be(original);
        }

        [Fact]
        public void ParsePointZ_ExposesOrdinatesAndFlags()
        {
            var point = (GeoPoint)GeoGeometry.Parse("POINT Z (1 2 3)");
            point.HasZ.Should().BeTrue();
            point.HasM.Should().BeFalse();
            point.Coordinate.Z.Should().Be(3);
        }

        [Fact]
        public void ParsePointM_ExposesOrdinatesAndFlags()
        {
            var point = (GeoPoint)GeoGeometry.Parse("POINT M (1 2 4)");
            point.HasZ.Should().BeFalse();
            point.HasM.Should().BeTrue();
            point.Coordinate.M.Should().Be(4);
        }

        [Fact]
        public void ParseUntagged_ThirdOrdinate_IsAutoDetectedAsZ()
        {
            var point = (GeoPoint)GeoGeometry.Parse("POINT (1 2 3)");
            point.HasZ.Should().BeTrue();
            point.HasM.Should().BeFalse();
            point.Coordinate.Z.Should().Be(3);
            point.ToWkt(includeSrid: false).Should().Be("POINT Z (1 2 3)");
        }

        [Fact]
        public void ParseUntagged_FourthOrdinate_IsAutoDetectedAsM()
        {
            var point = (GeoPoint)GeoGeometry.Parse("POINT (1 2 3 4)");
            point.HasZ.Should().BeTrue();
            point.HasM.Should().BeTrue();
            point.Coordinate.M.Should().Be(4);
        }

        [Fact]
        public void Read_IsoWkb_PointZ_IsSupported()
        {
            byte[] isoPointZ =
            {
                0x01,                                           // little-endian
                0xE9, 0x03, 0x00, 0x00,                         // type = 1001 (ISO PointZ)
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F, // X = 1.0
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, // Y = 2.0
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x40, // Z = 3.0
            };
            GeoGeometry.FromWkb(isoPointZ).Should().Be(new GeoPoint(new GeoCoordinate(1, 2, 3)));
        }

        [Fact]
        public void CreateXYM_RoundTripsAsMeasured()
        {
            var original = new GeoPoint(GeoCoordinate.CreateXYM(1, 2, 5));
            original.HasZ.Should().BeFalse();
            original.HasM.Should().BeTrue();
            original.ToWkt(includeSrid: false).Should().Be("POINT M (1 2 5)");

            GeoGeometry restored = GeoGeometry.FromWkb(original.ToWkb(), original.Srid);
            restored.Should().Be(original);
        }

        [Fact]
        public void Equality_DistinguishesDimensions()
        {
            new GeoPoint(1, 2).Should().NotBe(new GeoPoint(new GeoCoordinate(1, 2, 3)));
            new GeoPoint(new GeoCoordinate(1, 2, 3)).Should().NotBe(new GeoPoint(GeoCoordinate.CreateXYM(1, 2, 3)));
        }
    }
}

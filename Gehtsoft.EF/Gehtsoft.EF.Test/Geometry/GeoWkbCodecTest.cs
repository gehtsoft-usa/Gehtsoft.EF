using System;
using AwesomeAssertions;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geometry
{
    public class GeoWkbCodecTest
    {
        [Theory]
        [InlineData("POINT (1 2)")]
        [InlineData("POINT EMPTY")]
        [InlineData("LINESTRING (1 2, 3 4, 5 6)")]
        [InlineData("LINESTRING EMPTY")]
        [InlineData("POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0), (2 2, 2 4, 4 4, 4 2, 2 2))")]
        [InlineData("POLYGON EMPTY")]
        [InlineData("MULTIPOINT ((1 2), (3 4))")]
        [InlineData("MULTILINESTRING ((1 2, 3 4), (5 6, 7 8))")]
        [InlineData("MULTIPOLYGON (((0 0, 0 1, 1 1, 1 0, 0 0)), ((10 10, 10 11, 11 11, 11 10, 10 10)))")]
        [InlineData("GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (3 4, 5 6))")]
        [InlineData("GEOMETRYCOLLECTION EMPTY")]
        public void WkbRoundTrip_PreservesGeometry(string wkt)
        {
            GeoGeometry original = GeoGeometry.Parse(wkt);
            byte[] wkb = original.ToWkb();
            GeoGeometry restored = GeoGeometry.FromWkb(wkb, original.Srid);
            restored.Should().Be(original);
        }

        [Fact]
        public void Write_Point_ProducesGoldenLittleEndianWkb()
        {
            byte[] expected =
            {
                0x01,                                           // NDR (little-endian)
                0x01, 0x00, 0x00, 0x00,                         // type = 1 (point)
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F, // X = 1.0
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, // Y = 2.0
            };
            new GeoPoint(1, 2).ToWkb().Should().Equal(expected);
        }

        [Fact]
        public void Read_BigEndianPoint_IsSupported()
        {
            byte[] bigEndian =
            {
                0x00,                                           // XDR (big-endian)
                0x00, 0x00, 0x00, 0x01,                         // type = 1 (point)
                0x3F, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // X = 1.0
                0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Y = 2.0
            };
            GeoGeometry.FromWkb(bigEndian).Should().Be(new GeoPoint(1, 2));
        }

        [Fact]
        public void Read_MixedEndianCollection_IsSupported()
        {
            byte[] mixed =
            {
                0x01,                                           // outer: little-endian
                0x04, 0x00, 0x00, 0x00,                         // type = 4 (multipoint)
                0x02, 0x00, 0x00, 0x00,                         // 2 members
                // member 1: little-endian point (1 2)
                0x01,
                0x01, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xF0, 0x3F,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40,
                // member 2: big-endian point (3 4)
                0x00,
                0x00, 0x00, 0x00, 0x01,
                0x40, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x40, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };
            var expected = new GeoMultiPoint(new[] { new GeoPoint(1, 2), new GeoPoint(3, 4) });
            GeoGeometry.FromWkb(mixed).Should().Be(expected);
        }

        [Fact]
        public void Read_EmptyPointWkb_DecodesToEmptyPoint()
        {
            byte[] wkb = new GeoPoint(double.NaN, double.NaN).ToWkb();
            GeoGeometry restored = GeoGeometry.FromWkb(wkb);
            restored.Should().BeOfType<GeoPoint>();
            restored.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void Read_InvalidByteOrder_Throws()
        {
            byte[] wkb = { 0x05, 0x01, 0x00, 0x00, 0x00 };
            Action act = () => GeoGeometry.FromWkb(wkb);
            act.Should().Throw<GeoFormatException>();
        }

        [Fact]
        public void Read_UnknownTypeCode_Throws()
        {
            byte[] wkb = { 0x01, 0xFF, 0x00, 0x00, 0x00 };
            Action act = () => GeoGeometry.FromWkb(wkb);
            act.Should().Throw<GeoFormatException>();
        }

        [Fact]
        public void Read_Truncated_Throws()
        {
            byte[] wkb = { 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 };
            Action act = () => GeoGeometry.FromWkb(wkb);
            act.Should().Throw<GeoFormatException>();
        }

        [Fact]
        public void Read_TrailingBytes_Throws()
        {
            byte[] valid = new GeoPoint(1, 2).ToWkb();
            byte[] withTrailing = new byte[valid.Length + 1];
            Array.Copy(valid, withTrailing, valid.Length);
            Action act = () => GeoGeometry.FromWkb(withTrailing);
            act.Should().Throw<GeoFormatException>();
        }

        [Fact]
        public void Read_Null_Throws()
        {
            Action act = () => GeoGeometry.FromWkb(null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}

using System;
using AwesomeAssertions;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geometry
{
    public class GeoWktCodecTest
    {
        [Theory]
        [InlineData("POINT (1 2)")]
        [InlineData("POINT (-1.5 2.25)")]
        [InlineData("POINT EMPTY")]
        [InlineData("LINESTRING (1 2, 3 4, 5 6)")]
        [InlineData("LINESTRING EMPTY")]
        [InlineData("POLYGON ((0 0, 0 1, 1 1, 1 0, 0 0))")]
        [InlineData("POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0), (2 2, 2 4, 4 4, 4 2, 2 2))")]
        [InlineData("POLYGON EMPTY")]
        [InlineData("MULTIPOINT ((1 2), (3 4))")]
        [InlineData("MULTIPOINT EMPTY")]
        [InlineData("MULTILINESTRING ((1 2, 3 4), (5 6, 7 8))")]
        [InlineData("MULTILINESTRING EMPTY")]
        [InlineData("MULTIPOLYGON (((0 0, 0 1, 1 1, 1 0, 0 0)), ((10 10, 10 11, 11 11, 11 10, 10 10)))")]
        [InlineData("MULTIPOLYGON EMPTY")]
        [InlineData("GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (3 4, 5 6))")]
        [InlineData("GEOMETRYCOLLECTION (POINT EMPTY, MULTIPOLYGON (((0 0, 0 1, 1 1, 0 0))))")]
        [InlineData("GEOMETRYCOLLECTION EMPTY")]
        public void WktRoundTrip_IsCanonical(string wkt)
        {
            GeoGeometry geometry = GeoGeometry.Parse(wkt);
            geometry.ToWkt().Should().Be(wkt);
        }

        [Fact]
        public void Parse_LegacyMultiPointForm_EqualsCanonical()
        {
            GeoGeometry legacy = GeoGeometry.Parse("MULTIPOINT (1 2, 3 4)");
            GeoGeometry canonical = GeoGeometry.Parse("MULTIPOINT ((1 2), (3 4))");
            legacy.Should().Be(canonical);
            legacy.ToWkt().Should().Be("MULTIPOINT ((1 2), (3 4))");
        }

        [Fact]
        public void Parse_ToleratesWhitespace()
        {
            GeoGeometry geometry = GeoGeometry.Parse("  point   (  1   2 )  ");
            geometry.Should().Be(new GeoPoint(1, 2));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("FOO (1 2)")]
        [InlineData("POINT (1)")]
        [InlineData("POINT (1 2")]
        [InlineData("POINT 1 2)")]
        [InlineData("POINT ((1 2))")]
        [InlineData("POINT (1 2) EXTRA")]
        [InlineData("POLYGON (0 0, 1 1)")]
        [InlineData("POINT (1 x)")]
        public void Parse_Malformed_Throws(string wkt)
        {
            Action act = () => GeoGeometry.Parse(wkt);
            act.Should().Throw<GeoFormatException>();
        }

        [Fact]
        public void Parse_Null_Throws()
        {
            Action act = () => GeoGeometry.Parse(null);
            act.Should().Throw<ArgumentNullException>();
        }
    }
}

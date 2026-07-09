using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using AwesomeAssertions;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geometry
{
    public class GeoGeometryValueTest
    {
        [Fact]
        public void Equality_SameShape_AreEqual()
        {
            var a = new GeoPoint(1, 2);
            var b = new GeoPoint(1, 2);
            a.Should().Be(b);
            a.GetHashCode().Should().Be(b.GetHashCode());
            (a == b).Should().BeTrue();
            (a != b).Should().BeFalse();
        }

        [Fact]
        public void Equality_DifferentCoordinate_AreNotEqual()
        {
            new GeoPoint(1, 2).Should().NotBe(new GeoPoint(1, 3));
        }

        [Fact]
        public void Equality_DifferentSrid_AreNotEqual()
        {
            new GeoPoint(1, 2, 4326).Should().NotBe(new GeoPoint(1, 2, 3857));
        }

        [Fact]
        public void Equality_DifferentSubtype_AreNotEqual()
        {
            GeoGeometry point = new GeoPoint(1, 2);
            GeoGeometry line = new GeoLineString(new[] { new GeoCoordinate(1, 2), new GeoCoordinate(3, 4) });
            point.Should().NotBe(line);
        }

        [Fact]
        public void Equality_TwoEmptyPoints_AreEqual()
        {
            GeoPoint.Empty().Should().Be(GeoPoint.Empty());
        }

        [Fact]
        public void ToString_ReturnsWkt()
        {
            var point = new GeoPoint(1, 2);
            point.ToString().Should().Be(point.ToWkt());
            point.ToString().Should().Be("POINT (1 2)");
        }

        [Fact]
        public void Constructor_TakesDefensiveCopy()
        {
            var coordinates = new List<GeoCoordinate> { new GeoCoordinate(1, 2), new GeoCoordinate(3, 4) };
            var line = new GeoLineString(coordinates);
            coordinates.Add(new GeoCoordinate(5, 6));
            line.Coordinates.Count.Should().Be(2);
        }

        [Fact]
        public void Constructor_NullCoordinates_Throws()
        {
            Action act = () => new GeoLineString(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_NullMember_Throws()
        {
            Action act = () => new GeoMultiPoint(new GeoPoint[] { null });
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Srid_IsPreservedThroughBothCodecs()
        {
            const int srid = 3857;
            GeoGeometry parsed = GeoGeometry.Parse("POINT (1 2)", srid);
            parsed.Srid.Should().Be(srid);

            GeoGeometry fromWkb = GeoGeometry.FromWkb(parsed.ToWkb(), srid);
            fromWkb.Srid.Should().Be(srid);
            fromWkb.Should().Be(parsed);
        }

        [Fact]
        public void CrossCodec_WktToWkbToWkt_IsStable()
        {
            const string wkt = "POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0), (2 2, 2 4, 4 4, 4 2, 2 2))";
            GeoGeometry viaText = GeoGeometry.Parse(wkt);
            GeoGeometry viaBinary = GeoGeometry.FromWkb(viaText.ToWkb(), viaText.Srid);
            viaBinary.Should().Be(viaText);
            viaBinary.ToWkt().Should().Be(wkt);
        }

        [Theory]
        [InlineData(0.1, 0.2)]
        [InlineData(1.0 / 3.0, 2.0 / 3.0)]
        [InlineData(1e-300, 1e300)]
        [InlineData(-123456.789012345, 98765.4321098765)]
        public void Precision_SurvivesBothCodecs(double x, double y)
        {
            var original = new GeoPoint(x, y);

            GeoGeometry viaWkb = GeoGeometry.FromWkb(original.ToWkb(), original.Srid);
            ((GeoPoint)viaWkb).X.Should().Be(x);
            ((GeoPoint)viaWkb).Y.Should().Be(y);

            GeoGeometry viaWkt = GeoGeometry.Parse(original.ToWkt(), original.Srid);
            ((GeoPoint)viaWkt).X.Should().Be(x);
            ((GeoPoint)viaWkt).Y.Should().Be(y);
        }

        [Fact]
        public void Codec_IsCultureInvariant()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                GeoGeometry geometry = GeoGeometry.Parse("POINT (1.5 2.25)");
                geometry.ToWkt().Should().Be("POINT (1.5 2.25)");
                geometry.Should().Be(new GeoPoint(1.5, 2.25));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Theory]
        [InlineData(GeoGeometryType.Point)]
        [InlineData(GeoGeometryType.LineString)]
        [InlineData(GeoGeometryType.Polygon)]
        [InlineData(GeoGeometryType.MultiPoint)]
        [InlineData(GeoGeometryType.MultiLineString)]
        [InlineData(GeoGeometryType.MultiPolygon)]
        [InlineData(GeoGeometryType.GeometryCollection)]
        public void Empty_EverySubtype_RoundTrips(GeoGeometryType type)
        {
            GeoGeometry empty = CreateEmpty(type);
            empty.IsEmpty.Should().BeTrue();
            empty.GeometryType.Should().Be(type);

            GeoGeometry viaWkt = GeoGeometry.Parse(empty.ToWkt(), empty.Srid);
            viaWkt.Should().Be(empty);

            GeoGeometry viaWkb = GeoGeometry.FromWkb(empty.ToWkb(), empty.Srid);
            viaWkb.Should().Be(empty);
        }

        private static GeoGeometry CreateEmpty(GeoGeometryType type)
        {
            switch (type)
            {
                case GeoGeometryType.Point: return GeoPoint.Empty();
                case GeoGeometryType.LineString: return new GeoLineString(new List<GeoCoordinate>());
                case GeoGeometryType.Polygon: return new GeoPolygon(new List<List<GeoCoordinate>>());
                case GeoGeometryType.MultiPoint: return new GeoMultiPoint(new List<GeoPoint>());
                case GeoGeometryType.MultiLineString: return new GeoMultiLineString(new List<GeoLineString>());
                case GeoGeometryType.MultiPolygon: return new GeoMultiPolygon(new List<GeoPolygon>());
                case GeoGeometryType.GeometryCollection: return new GeoGeometryCollection(new List<GeoGeometry>());
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}

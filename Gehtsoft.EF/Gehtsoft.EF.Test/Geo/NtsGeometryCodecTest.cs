using AwesomeAssertions;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    public class NtsGeometryCodecTest
    {
        private const int Srid = 4326;
        private static readonly NtsGeometryCodec Codec = new NtsGeometryCodec();

        [Theory]
        [InlineData("POINT (1 2)")]
        [InlineData("LINESTRING (1 2, 3 4, 5 6)")]
        [InlineData("POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0), (2 2, 2 4, 4 4, 4 2, 2 2))")]
        [InlineData("MULTIPOINT ((1 2), (3 4))")]
        [InlineData("MULTILINESTRING ((1 2, 3 4), (5 6, 7 8))")]
        [InlineData("MULTIPOLYGON (((0 0, 0 1, 1 1, 1 0, 0 0)), ((10 10, 10 11, 11 11, 10 10)))")]
        [InlineData("GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (3 4, 5 6))")]
        [InlineData("POINT EMPTY")]
        [InlineData("POLYGON EMPTY")]
        public void WkbRoundTrip_PreservesGeometryAndSrid(string wkt)
        {
            Geometry original = Read(wkt);

            var restoredEwkb = (Geometry)Codec.FromWkb(Codec.ToWkb(original, includeSrid: true), srid: 9999);
            restoredEwkb.EqualsExact(original).Should().BeTrue();
            restoredEwkb.SRID.Should().Be(Srid);

            var restoredPlain = (Geometry)Codec.FromWkb(Codec.ToWkb(original, includeSrid: false), srid: Srid);
            restoredPlain.EqualsExact(original).Should().BeTrue();
            restoredPlain.SRID.Should().Be(Srid);
        }

        [Theory]
        [InlineData("POINT (1 2)")]
        [InlineData("LINESTRING (1 2, 3 4, 5 6)")]
        [InlineData("POLYGON ((0 0, 0 10, 10 10, 10 0, 0 0))")]
        [InlineData("GEOMETRYCOLLECTION (POINT (1 2), LINESTRING (3 4, 5 6))")]
        public void WktRoundTrip_PreservesGeometryAndSrid(string wkt)
        {
            Geometry original = Read(wkt);

            string ewkt = Codec.ToWkt(original, includeSrid: true);
            ewkt.Should().StartWith("SRID=4326;");

            var restored = (Geometry)Codec.FromWkt(ewkt, srid: 9999);
            restored.EqualsExact(original).Should().BeTrue();
            restored.SRID.Should().Be(Srid);
        }

        [Fact]
        public void WkbRoundTrip_PreservesZ()
        {
            GeometryFactory factory = NtsGeometryServices.Instance.CreateGeometryFactory(Srid);
            Point original = factory.CreatePoint(new CoordinateZ(1, 2, 3));

            var restored = (Point)Codec.FromWkb(Codec.ToWkb(original, includeSrid: true), 9999);
            restored.Coordinate.Z.Should().Be(3);
            restored.SRID.Should().Be(Srid);
        }

        [Fact]
        public void WkbRoundTrip_PreservesM()
        {
            GeometryFactory factory = NtsGeometryServices.Instance.CreateGeometryFactory(Srid);
            CoordinateSequence sequence = factory.CoordinateSequenceFactory.Create(1, Ordinates.XYM);
            sequence.SetX(0, 1);
            sequence.SetY(0, 2);
            sequence.SetM(0, 4);
            Point original = factory.CreatePoint(sequence);

            var restored = (Point)Codec.FromWkb(Codec.ToWkb(original, includeSrid: true), 9999);
            restored.CoordinateSequence.GetM(0).Should().Be(4);
        }

        private static Geometry Read(string wkt)
        {
            var reader = new WKTReader();
            Geometry geometry = reader.Read(wkt);
            geometry.SRID = Srid;
            return geometry;
        }
    }
}

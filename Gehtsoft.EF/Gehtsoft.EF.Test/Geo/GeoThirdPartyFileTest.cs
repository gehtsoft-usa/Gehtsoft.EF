using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Verifies the shipped NetTopologySuite codec reads real third-party output: a TIGER/Line county
    /// boundary exported by an external tool as both EWKT (an <c>SRID=4326;</c> prefix) and EWKB (the
    /// 0x20000000 SRID flag + embedded SRID). Both forms are embedded resources
    /// (<c>geo.playground.test.wkt</c> / <c>.wkb</c>); the two are the same dataset, so the geometries must
    /// match.
    /// </summary>
    public class GeoThirdPartyFileTest
    {
        private const int ExpectedSrid = 4326;
        private const int ExpectedPolygons = 6;
        private static readonly NtsGeometryCodec Codec = new NtsGeometryCodec();

        private static byte[] ReadResource(string logicalName)
        {
            using Stream stream = typeof(GeoThirdPartyFileTest).Assembly.GetManifestResourceStream(logicalName)
                ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' not found.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static string ReadText(string logicalName) => Encoding.UTF8.GetString(ReadResource(logicalName)).Trim();

        [Fact]
        public void ReadEwkt_ThirdPartyFile()
        {
            var geometry = (Geometry)Codec.FromWkt(ReadText("geo.playground.test.wkt"), srid: 9999);

            geometry.OgcGeometryType.Should().Be(OgcGeometryType.MultiPolygon);
            geometry.SRID.Should().Be(ExpectedSrid);
            geometry.NumGeometries.Should().Be(ExpectedPolygons);
        }

        [Fact]
        public void ReadEwkb_ThirdPartyFile()
        {
            var geometry = (Geometry)Codec.FromWkb(ReadResource("geo.playground.test.wkb"), srid: 9999);

            geometry.OgcGeometryType.Should().Be(OgcGeometryType.MultiPolygon);
            geometry.SRID.Should().Be(ExpectedSrid);
            geometry.NumGeometries.Should().Be(ExpectedPolygons);
        }

        [Fact]
        public void Ewkt_And_Ewkb_AreTheSameGeometry()
        {
            var fromText = (Geometry)Codec.FromWkt(ReadText("geo.playground.test.wkt"), ExpectedSrid);
            var fromBinary = (Geometry)Codec.FromWkb(ReadResource("geo.playground.test.wkb"), ExpectedSrid);

            fromBinary.EqualsExact(fromText).Should().BeTrue();
        }
    }
}

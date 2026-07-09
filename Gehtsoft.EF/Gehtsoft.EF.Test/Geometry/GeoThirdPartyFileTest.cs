using System;
using System.IO;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geometry
{
    /// <summary>
    /// Verifies the codec reads real third-party output: a TIGER/Line county boundary exported by an
    /// external tool as both EWKT (<c>test.wkt</c>, an <c>SRID=4326;</c> prefix) and EWKB
    /// (<c>test.wkb</c>, the 0x20000000 SRID flag + embedded SRID). Both are embedded resources; the
    /// two files are the same dataset, so the geometries must be identical.
    /// </summary>
    public class GeoThirdPartyFileTest
    {
        private const int ExpectedSrid = 4326;
        private const int ExpectedPolygons = 6;

        [Fact]
        public void ReadEwkt_ThirdPartyFile()
        {
            // A deliberately wrong default SRID proves the embedded EWKT prefix wins.
            GeoGeometry geometry = GeoGeometry.Parse(ReadTextResource("test.wkt"), srid: 9999);

            geometry.Should().BeOfType<GeoMultiPolygon>();
            geometry.Srid.Should().Be(ExpectedSrid);
            ((GeoMultiPolygon)geometry).Polygons.Count.Should().Be(ExpectedPolygons);
        }

        [Fact]
        public void ReadEwkb_ThirdPartyFile()
        {
            // A deliberately wrong default SRID proves the embedded EWKB SRID flag wins.
            GeoGeometry geometry = GeoGeometry.FromWkb(ReadBinaryResource("test.wkb"), srid: 9999);

            geometry.Should().BeOfType<GeoMultiPolygon>();
            geometry.Srid.Should().Be(ExpectedSrid);
            ((GeoMultiPolygon)geometry).Polygons.Count.Should().Be(ExpectedPolygons);
        }

        [Fact]
        public void Ewkt_And_Ewkb_AreTheSameGeometry()
        {
            GeoGeometry fromText = GeoGeometry.Parse(ReadTextResource("test.wkt"));
            GeoGeometry fromBinary = GeoGeometry.FromWkb(ReadBinaryResource("test.wkb"));

            fromBinary.Should().Be(fromText);
        }

        private static string ReadTextResource(string fileName)
        {
            using (Stream stream = OpenResource(fileName))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd().Trim();
        }

        private static byte[] ReadBinaryResource(string fileName)
        {
            using (Stream stream = OpenResource(fileName))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static Stream OpenResource(string fileName)
        {
            Assembly assembly = typeof(GeoThirdPartyFileTest).Assembly;
            string suffix = "." + fileName;
            foreach (string name in assembly.GetManifestResourceNames())
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                    return assembly.GetManifestResourceStream(name);
            throw new InvalidOperationException($"Embedded resource ending with '{suffix}' was not found.");
        }
    }
}

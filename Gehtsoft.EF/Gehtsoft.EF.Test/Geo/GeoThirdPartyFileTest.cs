using AwesomeAssertions;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Verifies the shipped NetTopologySuite codec reads real third-party output: a TIGER/Line county
    /// boundary exported by an external tool as both EWKT (<c>test.wkt</c>, an <c>SRID=4326;</c> prefix)
    /// and EWKB (<c>test.wkb</c>, the 0x20000000 SRID flag + embedded SRID). The files live in the
    /// Git-LFS-tracked <c>GeoTestData</c> folder (see <see cref="GeoTestData"/>); the two are the same
    /// dataset, so the geometries must match.
    /// </summary>
    public class GeoThirdPartyFileTest
    {
        private const int ExpectedSrid = 4326;
        private const int ExpectedPolygons = 6;
        private static readonly NtsGeometryCodec Codec = new NtsGeometryCodec();

        [Fact]
        public void ReadEwkt_ThirdPartyFile()
        {
            var geometry = (Geometry)Codec.FromWkt(GeoTestData.ReadAllText("test.wkt"), srid: 9999);

            geometry.OgcGeometryType.Should().Be(OgcGeometryType.MultiPolygon);
            geometry.SRID.Should().Be(ExpectedSrid);
            geometry.NumGeometries.Should().Be(ExpectedPolygons);
        }

        [Fact]
        public void ReadEwkb_ThirdPartyFile()
        {
            var geometry = (Geometry)Codec.FromWkb(GeoTestData.ReadAllBytes("test.wkb"), srid: 9999);

            geometry.OgcGeometryType.Should().Be(OgcGeometryType.MultiPolygon);
            geometry.SRID.Should().Be(ExpectedSrid);
            geometry.NumGeometries.Should().Be(ExpectedPolygons);
        }

        [Fact]
        public void Ewkt_And_Ewkb_AreTheSameGeometry()
        {
            var fromText = (Geometry)Codec.FromWkt(GeoTestData.ReadAllText("test.wkt"), ExpectedSrid);
            var fromBinary = (Geometry)Codec.FromWkb(GeoTestData.ReadAllBytes("test.wkb"), ExpectedSrid);

            fromBinary.EqualsExact(fromText).Should().BeTrue();
        }
    }
}

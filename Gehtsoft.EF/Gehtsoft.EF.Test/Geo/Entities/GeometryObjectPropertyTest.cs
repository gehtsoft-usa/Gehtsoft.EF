using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.IO;
using Xunit;
using NtsGeom = NetTopologySuite.Geometries.Geometry;

namespace Gehtsoft.EF.Test.Geo.Entities
{
    /// <summary>
    /// Declare tests for the object (NetTopologySuite) geometry-property path — the codec-backed
    /// decorating accessor. Shares the codec-registration collection so global codec state is serialized.
    /// </summary>
    [Collection("GeometryCodecRegistration")]
    public class GeometryObjectPropertyTest
    {
        [Entity(Scope = "geoobject", Table = "geo_object")]
        public class GeoObjectOwner
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "location", Srid = 4326)]
            public NtsGeom Location { get; set; }
        }

        private static TableDescriptor.ColumnInfo Column(TableDescriptor td, string id)
        {
            foreach (TableDescriptor.ColumnInfo c in td)
                if (c.ID == id)
                    return c;
            return null;
        }

        private static TableDescriptor.ColumnInfo LocationColumn()
        {
            NtsGeometry.Register();
            return Column(AllEntities.Inst[typeof(GeoObjectOwner)].TableDescriptor, "Location");
        }

        [Fact]
        public void ObjectGeometry_InstallsCodecBackedAccessor()
        {
            var location = LocationColumn();

            location.DbType.Should().Be(DbType.Binary);
            location.Geometry.Should().NotBeNull();
            location.Geometry.ClrType.Should().Be(typeof(NtsGeom));
            location.Geometry.Srid.Should().Be(4326);

            location.PropertyAccessor.Should().BeOfType<GeometryPropertyAccessor>();
            location.PropertyAccessor.PropertyType.Should().Be(typeof(byte[]));
        }

        [Fact]
        public void Accessor_RoundTripsGeometryThroughWkb()
        {
            var location = LocationColumn();
            var codec = new NtsGeometryCodec();

            NtsGeom point = new WKTReader().Read("POINT (1 2)");
            point.SRID = 4326;
            var owner = new GeoObjectOwner { Location = point };

            object wkb = location.PropertyAccessor.GetValue(owner);
            wkb.Should().BeOfType<byte[]>();
            ((byte[])wkb).Should().Equal(codec.ToWkb(point, false));

            var target = new GeoObjectOwner();
            location.PropertyAccessor.SetValue(target, wkb);
            target.Location.Should().NotBeNull();
            target.Location.EqualsExact(point).Should().BeTrue();
            target.Location.SRID.Should().Be(4326, "the declared SRID is applied on read");
        }

        [Fact]
        public void Accessor_HandlesNullBothWays()
        {
            var location = LocationColumn();

            location.PropertyAccessor.GetValue(new GeoObjectOwner()).Should().BeNull();

            var target = new GeoObjectOwner { Location = new WKTReader().Read("POINT (5 6)") };
            location.PropertyAccessor.SetValue(target, null);
            target.Location.Should().BeNull();
        }
    }
}

using System;
using AwesomeAssertions;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    [Collection("GeometryCodecRegistration")]
    public class GeometryCodecsRegistrationTest
    {
        [Fact]
        public void Resolve_WithoutFactory_Throws()
        {
            IGeometryCodecFactory previous = GeometryCodecs.Factory;
            try
            {
                GeometryCodecs.Factory = null;
                Action act = () => GeometryCodecs.Resolve();
                act.Should().Throw<InvalidOperationException>();
            }
            finally
            {
                GeometryCodecs.Factory = previous;
            }
        }

        [Fact]
        public void Register_InstallsNtsCodec()
        {
            IGeometryCodecFactory previous = GeometryCodecs.Factory;
            try
            {
                GeometryCodecs.Factory = null;
                NtsGeometry.Register();

                IGeometryCodec codec = GeometryCodecs.Resolve();
                codec.Should().BeOfType<NtsGeometryCodec>();
                codec.CanHandle(typeof(Point)).Should().BeTrue();
                codec.CanHandle(typeof(Geometry)).Should().BeTrue();
                codec.CanHandle(typeof(string)).Should().BeFalse();
            }
            finally
            {
                GeometryCodecs.Factory = previous;
            }
        }
    }
}

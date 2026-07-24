using Gehtsoft.EF.Entities.Geometry;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>Entry point for wiring the NetTopologySuite geometry codec into the framework.</summary>
    public static class NtsGeometry
    {
        /// <summary>Registers the NetTopologySuite codec as the global default geometry codec.</summary>
        /// <remarks>Sets <see cref="GeometryCodecs.Factory"/>; call once at application start-up.</remarks>
        public static void Register() => GeometryCodecs.Factory = new NtsGeometryCodecFactory();
    }
}

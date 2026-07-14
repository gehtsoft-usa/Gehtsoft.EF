using Gehtsoft.EF.Entities.Geometry;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>Entry point for wiring the NetTopologySuite geometry codec into the framework.</summary>
    public static class NtsGeometry
    {
        /// <summary>
        /// Registers the NetTopologySuite codec as the global default
        /// (<see cref="GeometryCodecs.Factory"/>). Call once at application start-up.
        /// </summary>
        public static void Register() => GeometryCodecs.Factory = new NtsGeometryCodecFactory();
    }
}

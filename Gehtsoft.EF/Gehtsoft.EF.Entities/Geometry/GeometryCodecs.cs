using System;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// The global registration point for the geometry codec. An application that maps geometry columns
    /// to object types sets <see cref="Factory"/> once at start-up (for example the NetTopologySuite
    /// module's registration helper); the framework then resolves a codec via <see cref="Resolve"/>.
    /// A per-connection override is available on the SQL connection. Applications that use raw
    /// <c>byte[]</c> (WKB) geometry properties never touch this class.
    /// </summary>
    public static class GeometryCodecs
    {
        /// <summary>
        /// The global default codec factory, or <c>null</c> when none is registered. Set this once at
        /// application start-up.
        /// </summary>
        public static IGeometryCodecFactory Factory { get; set; }

        /// <summary>
        /// Resolves the global codec from <see cref="Factory"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">No factory has been registered.</exception>
        public static IGeometryCodec Resolve()
        {
            IGeometryCodecFactory factory = Factory;
            if (factory == null)
                throw new InvalidOperationException(
                    "No geometry codec is registered. Reference the Gehtsoft.EF.Geo.NetTopologySuite module " +
                    "and call its registration helper, or set GeometryCodecs.Factory, or use a byte[] (WKB) geometry property.");
            IGeometryCodec codec = factory.Create();
            if (codec == null)
                throw new InvalidOperationException("The registered geometry codec factory returned null.");
            return codec;
        }
    }
}

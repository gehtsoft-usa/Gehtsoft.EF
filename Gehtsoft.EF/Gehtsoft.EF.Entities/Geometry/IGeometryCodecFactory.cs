namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Produces geometry codec instances for the framework.
    /// </summary>
    /// <remarks>
    /// An application registers a factory (for example the NetTopologySuite-backed one) via
    /// <see cref="GeometryCodecs.Factory"/> so the framework can map object-typed geometry properties to
    /// and from WKB. Applications that use raw <c>byte[]</c> (WKB) geometry properties need no factory.
    /// </remarks>
    public interface IGeometryCodecFactory
    {
        /// <summary>Creates (or returns) a geometry codec. Implementations may return a shared instance.</summary>
        IGeometryCodec Create();
    }
}

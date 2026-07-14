namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Produces an <see cref="IGeometryCodec"/>. An application registers a factory (for example the
    /// NetTopologySuite-backed one) via <see cref="GeometryCodecs.Factory"/> so the framework can map
    /// object-typed geometry properties to and from WKB. Applications that use raw <c>byte[]</c> (WKB)
    /// geometry properties need no factory.
    /// </summary>
    public interface IGeometryCodecFactory
    {
        /// <summary>Creates (or returns) a geometry codec. Implementations may return a shared instance.</summary>
        IGeometryCodec Create();
    }
}

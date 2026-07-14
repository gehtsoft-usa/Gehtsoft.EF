using Gehtsoft.EF.Entities.Geometry;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>An <see cref="IGeometryCodecFactory"/> that produces the shared <see cref="NtsGeometryCodec"/>.</summary>
    public sealed class NtsGeometryCodecFactory : IGeometryCodecFactory
    {
        private static readonly NtsGeometryCodec mCodec = new NtsGeometryCodec();

        /// <inheritdoc/>
        public IGeometryCodec Create() => mCodec;
    }
}

using Gehtsoft.EF.Entities.Geometry;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>A geometry codec factory that produces the shared NetTopologySuite codec.</summary>
    public sealed class NtsGeometryCodecFactory : IGeometryCodecFactory
    {
        private static readonly NtsGeometryCodec mCodec = new NtsGeometryCodec();

        /// <summary>Creates (or returns) a geometry codec; this factory returns a shared instance.</summary>
        public IGeometryCodec Create() => mCodec;
    }
}

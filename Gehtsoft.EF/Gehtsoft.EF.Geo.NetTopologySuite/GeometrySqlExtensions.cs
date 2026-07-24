using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using NetTopologySuite.Geometries;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>
    /// Convenience extension methods for moving NetTopologySuite geometry objects in and out of a query at the SQL-builder layer.
    /// </summary>
    /// <remarks>
    /// The core framework keeps a geometry value as portable WKB <c>byte[]</c>; these helpers encode/decode
    /// that <c>byte[]</c> through the NTS codec so application and test code can bind and read geometry
    /// objects directly. They live in the NTS module (not the core SQL layer) so core never depends on a
    /// geometry object model.
    /// </remarks>
    public static class GeometrySqlExtensions
    {
        private static readonly NtsGeometryCodec Codec = new NtsGeometryCodec();

        /// <summary>Binds a geometry parameter as plain OGC WKB bytes (a null geometry binds as SQL NULL).</summary>
        /// <remarks>
        /// The SRID is supplied separately by the SQL constructor function that wraps the parameter, so the
        /// WKB itself is emitted without an SRID.
        /// </remarks>
        /// <param name="query">The query to bind the parameter on.</param>
        /// <param name="name">The parameter name (without the dialect prefix).</param>
        /// <param name="value">The geometry to bind, or <c>null</c>.</param>
        public static void BindGeometryParam(this SqlDbQuery query, string name, Geometry value)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            byte[] wkb = value == null ? null : Codec.ToWkb(value, includeSrid: false);
            query.BindParam(name, DbType.Binary, wkb);
        }

        /// <summary>Reads a geometry from a WKB byte[] resultset column by index (returns null when the column is NULL).</summary>
        /// <remarks>
        /// The column must have been selected through the dialect's WKB output wrapper (for example <c>ST_AsBinary</c>).
        /// </remarks>
        /// <param name="query">The query positioned on a row.</param>
        /// <param name="column">The zero-based resultset column index.</param>
        /// <param name="srid">The SRID to assign when the WKB does not embed one.</param>
        public static Geometry GetGeometry(this SqlDbQuery query, int column, int srid = 0)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            byte[] wkb = query.GetValue<byte[]>(column);
            return wkb == null ? null : (Geometry)Codec.FromWkb(wkb, srid);
        }

        /// <summary>
        /// Reads a geometry from a WKB byte[] resultset column by name (see the index overload).
        /// </summary>
        /// <param name="query">The query positioned on a row.</param>
        /// <param name="column">The resultset column name (or alias).</param>
        /// <param name="srid">The SRID to assign when the WKB does not embed one.</param>
        public static Geometry GetGeometry(this SqlDbQuery query, string column, int srid = 0)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            byte[] wkb = query.GetValue<byte[]>(column);
            return wkb == null ? null : (Geometry)Codec.FromWkb(wkb, srid);
        }
    }
}

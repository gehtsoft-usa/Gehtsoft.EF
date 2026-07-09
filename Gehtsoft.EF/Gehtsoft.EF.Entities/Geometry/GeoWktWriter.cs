using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Writes a geometry to Well-Known Text (WKT). Output is deterministic and canonical: parenthesized
    /// MULTIPOINT members, <c>EMPTY</c> for empty geometries, invariant-culture numbers with round-trip
    /// double precision, and an ISO dimensional tag (<c>Z</c> / <c>M</c> / <c>ZM</c>) whenever the
    /// geometry carries Z or M ordinates. By default a PostGIS <b>EWKT</b> <c>SRID=&lt;n&gt;;</c> prefix
    /// is written; pass <c>includeSrid: false</c> to omit it.
    /// </summary>
    public sealed class GeoWktWriter
    {
        /// <summary>Writes the geometry as WKT.</summary>
        /// <param name="geometry">The geometry to write.</param>
        /// <param name="includeSrid">When true (default), prepend the EWKT <c>SRID=&lt;n&gt;;</c> prefix.</param>
        public string Write(GeoGeometry geometry, bool includeSrid = true)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            var builder = new StringBuilder();
            if (includeSrid)
                builder.Append("SRID=").Append(geometry.Srid.ToString(CultureInfo.InvariantCulture)).Append(';');
            WriteGeometry(builder, geometry);
            return builder.ToString();
        }

        private void WriteGeometry(StringBuilder builder, GeoGeometry geometry)
        {
            bool hasZ = geometry.HasZ;
            bool hasM = geometry.HasM;
            switch (geometry.GeometryType)
            {
                case GeoGeometryType.Point: WritePoint(builder, (GeoPoint)geometry, hasZ, hasM); break;
                case GeoGeometryType.LineString: WriteLineString(builder, (GeoLineString)geometry, hasZ, hasM); break;
                case GeoGeometryType.Polygon: WritePolygon(builder, (GeoPolygon)geometry, hasZ, hasM); break;
                case GeoGeometryType.MultiPoint: WriteMultiPoint(builder, (GeoMultiPoint)geometry, hasZ, hasM); break;
                case GeoGeometryType.MultiLineString: WriteMultiLineString(builder, (GeoMultiLineString)geometry, hasZ, hasM); break;
                case GeoGeometryType.MultiPolygon: WriteMultiPolygon(builder, (GeoMultiPolygon)geometry, hasZ, hasM); break;
                case GeoGeometryType.GeometryCollection: WriteGeometryCollection(builder, (GeoGeometryCollection)geometry); break;
                default: throw new GeoFormatException($"Unsupported geometry type '{geometry.GeometryType}'.");
            }
        }

        private void WritePoint(StringBuilder builder, GeoPoint point, bool hasZ, bool hasM)
        {
            AppendKeyword(builder, "POINT", hasZ, hasM);
            if (point.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            builder.Append('(');
            AppendCoordinate(builder, point.Coordinate, hasZ, hasM);
            builder.Append(')');
        }

        private void WriteLineString(StringBuilder builder, GeoLineString line, bool hasZ, bool hasM)
        {
            AppendKeyword(builder, "LINESTRING", hasZ, hasM);
            if (line.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            AppendCoordinateList(builder, line.Coordinates, hasZ, hasM);
        }

        private void WritePolygon(StringBuilder builder, GeoPolygon polygon, bool hasZ, bool hasM)
        {
            AppendKeyword(builder, "POLYGON", hasZ, hasM);
            if (polygon.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            AppendRings(builder, polygon.Rings, hasZ, hasM);
        }

        private void WriteMultiPoint(StringBuilder builder, GeoMultiPoint multi, bool hasZ, bool hasM)
        {
            AppendKeyword(builder, "MULTIPOINT", hasZ, hasM);
            if (multi.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            builder.Append('(');
            for (int i = 0; i < multi.Points.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                GeoPoint point = multi.Points[i];
                if (point.IsEmpty)
                {
                    builder.Append("EMPTY");
                    continue;
                }
                builder.Append('(');
                AppendCoordinate(builder, point.Coordinate, hasZ, hasM);
                builder.Append(')');
            }
            builder.Append(')');
        }

        private void WriteMultiLineString(StringBuilder builder, GeoMultiLineString multi, bool hasZ, bool hasM)
        {
            AppendKeyword(builder, "MULTILINESTRING", hasZ, hasM);
            if (multi.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            builder.Append('(');
            for (int i = 0; i < multi.LineStrings.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                GeoLineString line = multi.LineStrings[i];
                if (line.IsEmpty)
                {
                    builder.Append("EMPTY");
                    continue;
                }
                AppendCoordinateList(builder, line.Coordinates, hasZ, hasM);
            }
            builder.Append(')');
        }

        private void WriteMultiPolygon(StringBuilder builder, GeoMultiPolygon multi, bool hasZ, bool hasM)
        {
            AppendKeyword(builder, "MULTIPOLYGON", hasZ, hasM);
            if (multi.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            builder.Append('(');
            for (int i = 0; i < multi.Polygons.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                GeoPolygon polygon = multi.Polygons[i];
                if (polygon.IsEmpty)
                {
                    builder.Append("EMPTY");
                    continue;
                }
                AppendRings(builder, polygon.Rings, hasZ, hasM);
            }
            builder.Append(')');
        }

        private void WriteGeometryCollection(StringBuilder builder, GeoGeometryCollection collection)
        {
            AppendKeyword(builder, "GEOMETRYCOLLECTION", collection.HasZ, collection.HasM);
            if (collection.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            builder.Append('(');
            for (int i = 0; i < collection.Geometries.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                WriteGeometry(builder, collection.Geometries[i]);
            }
            builder.Append(')');
        }

        private static void AppendKeyword(StringBuilder builder, string keyword, bool hasZ, bool hasM)
        {
            builder.Append(keyword).Append(' ');
            if (hasZ && hasM)
                builder.Append("ZM ");
            else if (hasZ)
                builder.Append("Z ");
            else if (hasM)
                builder.Append("M ");
        }

        private static void AppendRings(StringBuilder builder, IReadOnlyList<IReadOnlyList<GeoCoordinate>> rings, bool hasZ, bool hasM)
        {
            builder.Append('(');
            for (int i = 0; i < rings.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendCoordinateList(builder, rings[i], hasZ, hasM);
            }
            builder.Append(')');
        }

        private static void AppendCoordinateList(StringBuilder builder, IReadOnlyList<GeoCoordinate> coordinates, bool hasZ, bool hasM)
        {
            builder.Append('(');
            for (int i = 0; i < coordinates.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendCoordinate(builder, coordinates[i], hasZ, hasM);
            }
            builder.Append(')');
        }

        private static void AppendCoordinate(StringBuilder builder, GeoCoordinate coordinate, bool hasZ, bool hasM)
        {
            builder.Append(GeoCoordinate.Format(coordinate.X)).Append(' ').Append(GeoCoordinate.Format(coordinate.Y));
            if (hasZ)
                builder.Append(' ').Append(GeoCoordinate.Format(coordinate.Z));
            if (hasM)
                builder.Append(' ').Append(GeoCoordinate.Format(coordinate.M));
        }
    }
}

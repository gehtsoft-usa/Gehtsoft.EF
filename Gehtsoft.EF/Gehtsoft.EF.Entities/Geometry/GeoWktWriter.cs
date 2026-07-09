using System;
using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Writes a geometry to its OGC Well-Known Text (WKT) representation. Output is deterministic and
    /// canonical (parenthesized MULTIPOINT members, <c>EMPTY</c> for empty geometries), formatted in
    /// the invariant culture with round-trip double precision.
    /// </summary>
    public sealed class GeoWktWriter
    {
        /// <summary>Writes the geometry as WKT.</summary>
        /// <param name="geometry">The geometry to write.</param>
        public string Write(GeoGeometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            var builder = new StringBuilder();
            WriteGeometry(builder, geometry);
            return builder.ToString();
        }

        private void WriteGeometry(StringBuilder builder, GeoGeometry geometry)
        {
            switch (geometry.GeometryType)
            {
                case GeoGeometryType.Point: WritePoint(builder, (GeoPoint)geometry); break;
                case GeoGeometryType.LineString: WriteLineString(builder, (GeoLineString)geometry); break;
                case GeoGeometryType.Polygon: WritePolygon(builder, (GeoPolygon)geometry); break;
                case GeoGeometryType.MultiPoint: WriteMultiPoint(builder, (GeoMultiPoint)geometry); break;
                case GeoGeometryType.MultiLineString: WriteMultiLineString(builder, (GeoMultiLineString)geometry); break;
                case GeoGeometryType.MultiPolygon: WriteMultiPolygon(builder, (GeoMultiPolygon)geometry); break;
                case GeoGeometryType.GeometryCollection: WriteGeometryCollection(builder, (GeoGeometryCollection)geometry); break;
                default: throw new GeoFormatException($"Unsupported geometry type '{geometry.GeometryType}'.");
            }
        }

        private void WritePoint(StringBuilder builder, GeoPoint point)
        {
            builder.Append("POINT ");
            if (point.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            builder.Append('(');
            AppendCoordinate(builder, point.Coordinate);
            builder.Append(')');
        }

        private void WriteLineString(StringBuilder builder, GeoLineString line)
        {
            builder.Append("LINESTRING ");
            if (line.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            AppendCoordinateList(builder, line.Coordinates);
        }

        private void WritePolygon(StringBuilder builder, GeoPolygon polygon)
        {
            builder.Append("POLYGON ");
            if (polygon.IsEmpty)
            {
                builder.Append("EMPTY");
                return;
            }
            AppendRings(builder, polygon.Rings);
        }

        private void WriteMultiPoint(StringBuilder builder, GeoMultiPoint multi)
        {
            builder.Append("MULTIPOINT ");
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
                AppendCoordinate(builder, point.Coordinate);
                builder.Append(')');
            }
            builder.Append(')');
        }

        private void WriteMultiLineString(StringBuilder builder, GeoMultiLineString multi)
        {
            builder.Append("MULTILINESTRING ");
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
                AppendCoordinateList(builder, line.Coordinates);
            }
            builder.Append(')');
        }

        private void WriteMultiPolygon(StringBuilder builder, GeoMultiPolygon multi)
        {
            builder.Append("MULTIPOLYGON ");
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
                AppendRings(builder, polygon.Rings);
            }
            builder.Append(')');
        }

        private void WriteGeometryCollection(StringBuilder builder, GeoGeometryCollection collection)
        {
            builder.Append("GEOMETRYCOLLECTION ");
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

        private static void AppendRings(StringBuilder builder, IReadOnlyList<IReadOnlyList<GeoCoordinate>> rings)
        {
            builder.Append('(');
            for (int i = 0; i < rings.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendCoordinateList(builder, rings[i]);
            }
            builder.Append(')');
        }

        private static void AppendCoordinateList(StringBuilder builder, IReadOnlyList<GeoCoordinate> coordinates)
        {
            builder.Append('(');
            for (int i = 0; i < coordinates.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");
                AppendCoordinate(builder, coordinates[i]);
            }
            builder.Append(')');
        }

        private static void AppendCoordinate(StringBuilder builder, GeoCoordinate coordinate)
        {
            builder.Append(coordinate.ToString());
        }
    }
}

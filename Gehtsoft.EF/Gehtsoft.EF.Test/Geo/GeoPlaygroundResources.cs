using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Loads the minimized geo playground datasets from the test assembly's embedded resources
    /// (<c>geo.playground.{states,cities,tracks}.tsv</c>) — tab-separated, one feature per line, geometry
    /// as plain OGC WKT. The tests thus have NO runtime dependency on the raw <c>GeoTestData</c> folder.
    ///
    /// The resources were produced offline from <c>CLAUDE/GEO/GeoTestData</c>: US state polygons grouped by
    /// <c>ABBREV</c> into one MultiPolygon each, OziExplorer <c>.map</c> corners into city map-extent
    /// rectangles, and OziExplorer <c>.plt</c> logs into MultiLineString tracks — all decimated with NTS
    /// topology-preserving / Douglas-Peucker simplification at ~0.01° (~1 km). All geometries are SRID 4326.
    /// </summary>
    internal static class GeoPlaygroundResources
    {
        public const int Srid = 4326;
        private static readonly NtsGeometryCodec Codec = new NtsGeometryCodec();

        public sealed class StateRow
        {
            public string Abbrev { get; set; }
            public string Name { get; set; }
            public string Region { get; set; }
            public double AreaAttr { get; set; }
            public long Pop1999 { get; set; }
            public Geometry Shape { get; set; }
        }

        public sealed class CityRow
        {
            public string Name { get; set; }
            public Geometry Extent { get; set; }
            public Geometry Center { get; set; }
        }

        public sealed class TrackRow
        {
            public string Name { get; set; }
            public Geometry Path { get; set; }
        }

        public static List<StateRow> States()
        {
            var list = new List<StateRow>();
            foreach (string[] f in Rows("geo.playground.states.tsv"))
                list.Add(new StateRow
                {
                    Abbrev = f[0],
                    Name = f[1],
                    Region = f[2],
                    AreaAttr = double.Parse(f[3], CultureInfo.InvariantCulture),
                    Pop1999 = long.Parse(f[4], CultureInfo.InvariantCulture),
                    Shape = Parse(f[5]),
                });
            return list;
        }

        public static List<CityRow> Cities()
        {
            var list = new List<CityRow>();
            foreach (string[] f in Rows("geo.playground.cities.tsv"))
                list.Add(new CityRow { Name = f[0], Extent = Parse(f[1]), Center = Parse(f[2]) });
            return list;
        }

        public static List<TrackRow> Tracks()
        {
            var list = new List<TrackRow>();
            foreach (string[] f in Rows("geo.playground.tracks.tsv"))
                list.Add(new TrackRow { Name = f[0], Path = Parse(f[1]) });
            return list;
        }

        private static Geometry Parse(string wkt)
        {
            var geometry = (Geometry)Codec.FromWkt(wkt, Srid);
            geometry.SRID = Srid;
            return geometry;
        }

        private static IEnumerable<string[]> Rows(string logicalName)
        {
            using Stream stream = typeof(GeoPlaygroundResources).Assembly.GetManifestResourceStream(logicalName)
                ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' not found.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string text = reader.ReadToEnd();
            foreach (string line in text.Split('\n'))
                if (line.Length > 0)
                    yield return line.Split('\t');
        }
    }
}

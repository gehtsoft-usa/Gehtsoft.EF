using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// The LINQ twin of <see cref="GeoPlaygroundEntitySpatialiteTest"/>: the same real-world tasks over the
    /// same US datasets, now expressed as LINQ queries over <c>connection.GetCollectionOf&lt;T&gt;()</c> using
    /// the <see cref="SqlSpatial"/> marker methods (Contains/Intersects/DWithin/Distance/Area/Length) in
    /// Where / OrderBy / Select / GroupBy. Geometry operands are supplied as WKB <c>byte[]</c> (encoded at the
    /// call site with the NTS codec), so the LINQ compiler stays core/NTS-free. Live SpatiaLite; data is
    /// loaded through the entity insert path. Ordering in this LINQ provider is ascending-only, so the
    /// area/length checks assert the ascending order (smallest first, largest last).
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeoPlaygroundLinqSpatialiteTest
    {
        [Entity(Scope = "geo_lpg_state", Table = "lpg_state")]
        public class LpgState
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "abbrev", Size = 2)] public string Abbrev { get; set; }
            [EntityProperty(Field = "name", Size = 64)] public string Name { get; set; }
            [EntityProperty(Field = "region", Size = 32)] public string Region { get; set; }
            [EntityProperty(Field = "area")] public double AreaAttr { get; set; }
            [EntityProperty(Field = "pop")] public long Pop { get; set; }
            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry)] public byte[] Shape { get; set; }
        }

        [Entity(Scope = "geo_lpg_city", Table = "lpg_city")]
        public class LpgCity
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "name", Size = 40)] public string Name { get; set; }
            [GeometryEntityProperty(Field = "extent", Subtype = GeometrySubtype.Polygon)] public byte[] Extent { get; set; }
            [GeometryEntityProperty(Field = "center", Subtype = GeometrySubtype.Point)] public byte[] Center { get; set; }
        }

        [Entity(Scope = "geo_lpg_track", Table = "lpg_track")]
        public class LpgTrack
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "name", Size = 16)] public string Name { get; set; }
            [GeometryEntityProperty(Field = "path", Subtype = GeometrySubtype.MultiLineString)] public byte[] Path { get; set; }
        }

        [Fact]
        public void Playground()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                Setup(connection);

                StatesByArea(connection);
                CityInWhichState(connection);
                StatesCrossedByTrack(connection);
                NearestAndWithinDistance(connection);
                ProjectionAndGroupBy(connection);
            });
        }

        private static byte[] Wkb(Geometry g) => GeometryRoundTripSupport.ToWkb(g);

        // ---- task 1: states ordered by area (ascending: smallest first, largest last) ----

        private static void StatesByArea(SqlDbConnection connection)
        {
            var byArea = connection.GetCollectionOf<LpgState>()
                .OrderBy(s => SqlSpatial.Area(s.Shape))
                .Select(s => new { s.Abbrev, Area = SqlSpatial.Area(s.Shape) })
                .ToList();

            byArea.Should().HaveCount(22);
            for (int i = 1; i < byArea.Count; i++)
                byArea[i].Area.Should().BeGreaterThanOrEqualTo(byArea[i - 1].Area);
            byArea[0].Abbrev.Should().BeOneOf("DC", "RI", "DE");
            byArea[byArea.Count - 1].Abbrev.Should().BeOneOf("NY", "NC", "FL", "GA", "ME", "VA", "PA");
        }

        // ---- task 2: which state CONTAINS each city centre ----

        private static void CityInWhichState(SqlDbConnection connection)
        {
            var found = new Dictionary<string, List<string>>();
            foreach (GeoPlaygroundResources.CityRow city in GeoPlaygroundResources.Cities())
            {
                byte[] centre = Wkb(city.Center);
                var hits = connection.GetCollectionOf<LpgState>()
                    .Where(s => SqlSpatial.Contains(s.Shape, centre))
                    .Select(s => s.Abbrev)
                    .ToList();
                found[city.Name] = hits;
            }

            found["charlotte-nc-z17"].Should().Contain("NC");
            found["wilmington-nc-z17"].Should().Contain("NC");
            found["emerald-isle-nc-z17"].Should().Contain("NC");
            found["richmond-va-z17"].Should().Contain("VA");
        }

        // ---- task 3: which states does each track cross ----

        private static void StatesCrossedByTrack(SqlDbConnection connection)
        {
            var crossed = new Dictionary<string, List<string>>();
            foreach (GeoPlaygroundResources.TrackRow track in GeoPlaygroundResources.Tracks())
            {
                byte[] path = Wkb(track.Path);
                var hits = connection.GetCollectionOf<LpgState>()
                    .Where(s => SqlSpatial.Intersects(s.Shape, path))
                    .OrderBy(s => SqlSpatial.Area(s.Shape))
                    .Select(s => s.Abbrev)
                    .ToList();
                crossed[track.Name] = hits;
            }

            crossed["nc2tx"].Should().Contain("NC");
            crossed["nj2nc"].Should().Contain(new[] { "NJ", "NC" });
            crossed["nj2oh"].Should().Contain(new[] { "NJ", "PA", "OH" });
        }

        // ---- task 4: nearest city (order-by-distance + Take) and within-distance (DWithin) ----

        private static void NearestAndWithinDistance(SqlDbConnection connection)
        {
            byte[] probe = Wkb(new Point(-74.0, 40.7) { SRID = GeoPlaygroundResources.Srid });
            var nearest = connection.GetCollectionOf<LpgCity>()
                .OrderBy(c => SqlSpatial.Distance(c.Center, probe))
                .Take(3)
                .Select(c => c.Name)
                .ToList();
            nearest[0].Should().Be("new-york-ny-z17");

            byte[] nj2nc = Wkb(FindTrack("nj2nc").Path);
            const double threshold = 1.0;

            var distances = connection.GetCollectionOf<LpgCity>()
                .Select(c => new { c.Name, D = SqlSpatial.Distance(c.Center, nj2nc) })
                .ToList();

            var within = connection.GetCollectionOf<LpgCity>()
                .Where(c => SqlSpatial.DWithin(c.Center, nj2nc, threshold))
                .Select(c => c.Name)
                .ToList();

            var expectedWithin = distances.Where(x => x.D <= threshold).Select(x => x.Name).ToList();
            within.Should().BeEquivalentTo(expectedWithin);
        }

        // ---- task 5: projection (Length, ascending) + group-by region with COUNT and AVG(Area) ----

        private static void ProjectionAndGroupBy(SqlDbConnection connection)
        {
            var lengths = connection.GetCollectionOf<LpgTrack>()
                .OrderBy(t => SqlSpatial.Length(t.Path))
                .Select(t => t.Name)
                .ToList();
            lengths[lengths.Count - 1].Should().Be("nc2tx");   // longest track is last (ascending order)

            var perRegion = connection.GetCollectionOf<LpgState>()
                .GroupBy(s => s.Region)
                .Select(g => new { Region = g.Key, Cnt = g.Count(), Avg = g.Average(s => SqlSpatial.Area(s.Shape)) })
                .ToList();
            perRegion.Single(r => r.Region == "New England").Cnt.Should().Be(6);
        }

        private static GeoPlaygroundResources.TrackRow FindTrack(string name)
        {
            foreach (GeoPlaygroundResources.TrackRow t in GeoPlaygroundResources.Tracks())
                if (t.Name == name)
                    return t;
            return null;
        }

        private static void Setup(SqlDbConnection connection)
        {
            using (var q = connection.GetCreateEntityQuery<LpgState>()) q.Execute();
            using (var q = connection.GetCreateEntityQuery<LpgCity>()) q.Execute();
            using (var q = connection.GetCreateEntityQuery<LpgTrack>()) q.Execute();

            foreach (GeoPlaygroundResources.StateRow s in GeoPlaygroundResources.States())
            {
                var e = new LpgState
                {
                    Abbrev = s.Abbrev, Name = s.Name, Region = s.Region, AreaAttr = s.AreaAttr, Pop = s.Pop1999,
                    Shape = Wkb(s.Shape),
                };
                using var q = connection.GetInsertEntityQuery<LpgState>();
                q.Execute(e);
            }

            foreach (GeoPlaygroundResources.CityRow c in GeoPlaygroundResources.Cities())
            {
                var e = new LpgCity { Name = c.Name, Extent = Wkb(c.Extent), Center = Wkb(c.Center) };
                using var q = connection.GetInsertEntityQuery<LpgCity>();
                q.Execute(e);
            }

            foreach (GeoPlaygroundResources.TrackRow t in GeoPlaygroundResources.Tracks())
            {
                var e = new LpgTrack { Name = t.Name, Path = Wkb(t.Path) };
                using var q = connection.GetInsertEntityQuery<LpgTrack>();
                q.Execute(e);
            }
        }
    }
}

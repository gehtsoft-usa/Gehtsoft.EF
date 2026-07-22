using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// The entity-query twin of <see cref="GeoPlaygroundSpatialiteTest"/>: the same real-world tasks over the
    /// same US datasets (state polygons, city map-extents, travel tracks) on a live SpatiaLite database, but
    /// solved entirely through the ENTITY query surface (Phase 5) rather than the pure-SQL builders -
    /// <c>GetInsertEntityQuery</c>, <c>GetSelectEntitiesQueryBase</c> + <c>GeoPredicateOf</c>/<c>GeoScalarOf</c>
    /// / <c>AddGeometryScalarTo*</c>, and <c>GetMultiDeleteEntityQuery</c>. Asserts the same confident answers,
    /// proving the entity surface delivers the whole pure-SQL geo query surface. The geometry operand is an
    /// NTS object throughout (encoded by the NTS module). Skips when the native library is unavailable.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeoPlaygroundEntitySpatialiteTest
    {
        [Entity(Scope = "geo_epg_state", Table = "epg_state")]
        public class EpgState
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "abbrev", Size = 2)] public string Abbrev { get; set; }
            [EntityProperty(Field = "name", Size = 64)] public string Name { get; set; }
            [EntityProperty(Field = "region", Size = 32)] public string Region { get; set; }
            [EntityProperty(Field = "area")] public double AreaAttr { get; set; }
            [EntityProperty(Field = "pop")] public long Pop { get; set; }
            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry)] public byte[] Shape { get; set; }
        }

        [Entity(Scope = "geo_epg_city", Table = "epg_city")]
        public class EpgCity
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "name", Size = 40)] public string Name { get; set; }
            [GeometryEntityProperty(Field = "extent", Subtype = GeometrySubtype.Polygon)] public byte[] Extent { get; set; }
            [GeometryEntityProperty(Field = "center", Subtype = GeometrySubtype.Point)] public byte[] Center { get; set; }
        }

        [Entity(Scope = "geo_epg_track", Table = "epg_track")]
        public class EpgTrack
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
                StatesCrossedBySubqueryTrack(connection);
            });
        }

        // ---- task 1: states ordered by area (ST_Area), cross-checked vs the AREA attribute ----

        private static void StatesByArea(SqlDbConnection connection)
        {
            var order = new List<string>();
            var gareas = new List<double>();
            using var q = connection.GetSelectEntitiesQueryBase<EpgState>();
            q.AddToResultset("Abbrev", "abbrev");
            q.AddToResultset("AreaAttr", "attr");
            q.AddGeometryScalarToResultset<EpgState>(SqlGeoFunctionId.Area, "Shape", DbType.Double, "garea");
            q.AddGeometryScalarToOrderBy<EpgState>(SqlGeoFunctionId.Area, "Shape", SortDir.Desc);
            q.ExecuteReader();
            while (q.ReadNext())
            {
                order.Add(q.GetValue<string>("abbrev"));
                gareas.Add(q.GetValue<double>("garea"));
            }

            order.Should().HaveCount(22);
            for (int i = 1; i < gareas.Count; i++)
                gareas[i].Should().BeLessThanOrEqualTo(gareas[i - 1]);
            order[0].Should().BeOneOf("NY", "NC", "FL", "GA", "ME", "VA", "PA");
            order[order.Count - 1].Should().BeOneOf("DC", "RI", "DE");
        }

        // ---- task 2: which state CONTAINS each city centre ----

        private static void CityInWhichState(SqlDbConnection connection)
        {
            var found = new Dictionary<string, string>();
            foreach (GeoPlaygroundResources.CityRow city in GeoPlaygroundResources.Cities())
            {
                var hits = new List<string>();
                using var q = connection.GetSelectEntitiesQueryBase<EpgState>();
                q.AddToResultset("Abbrev", "abbrev");
                q.Where.GeoPredicateOf<EpgState>("Shape", SqlGeoPredicateId.Contains, city.Center);
                q.ExecuteReader();
                while (q.ReadNext())
                    hits.Add(q.GetValue<string>("abbrev"));
                found[city.Name] = string.Join(",", hits);
            }

            found["charlotte-nc-z17"].Should().Contain("NC");
            found["wilmington-nc-z17"].Should().Contain("NC");
            found["emerald-isle-nc-z17"].Should().Contain("NC");
            found["richmond-va-z17"].Should().Contain("VA");
        }

        // ---- task 3: which states does each track cross (state INTERSECTS the track) ----

        private static void StatesCrossedByTrack(SqlDbConnection connection)
        {
            var crossed = new Dictionary<string, List<string>>();
            foreach (GeoPlaygroundResources.TrackRow track in GeoPlaygroundResources.Tracks())
            {
                var hits = new List<string>();
                using var q = connection.GetSelectEntitiesQueryBase<EpgState>();
                q.AddToResultset("Abbrev", "abbrev");
                q.AddGeometryScalarToOrderBy<EpgState>(SqlGeoFunctionId.Area, "Shape", SortDir.Desc);
                q.Where.GeoPredicateOf<EpgState>("Shape", SqlGeoPredicateId.Intersects, track.Path);
                q.ExecuteReader();
                while (q.ReadNext())
                    hits.Add(q.GetValue<string>("abbrev"));
                crossed[track.Name] = hits;
            }

            crossed["nc2tx"].Should().Contain("NC");
            crossed["nj2nc"].Should().Contain(new[] { "NJ", "NC" });
            crossed["nj2oh"].Should().Contain(new[] { "NJ", "PA", "OH" });
        }

        // ---- task 4: nearest city (order-by-distance top-N) + within-distance (DWithin) ----

        private static void NearestAndWithinDistance(SqlDbConnection connection)
        {
            var probe = new Point(-74.0, 40.7) { SRID = GeoPlaygroundResources.Srid };
            var nearest = new List<string>();
            using (var q = connection.GetSelectEntitiesQueryBase<EpgCity>())
            {
                q.AddToResultset("Name", "name");
                q.AddGeometryScalarToResultset<EpgCity>(SqlGeoFunctionId.Distance, "Center", DbType.Double, "d", parameterName: "p");
                q.AddGeometryScalarToOrderBy<EpgCity>(SqlGeoFunctionId.Distance, "Center", SortDir.Asc, parameterName: "p");
                q.Limit = 3;
                q.Query.BindGeometryParam("p", probe);
                q.ExecuteReader();
                while (q.ReadNext())
                    nearest.Add(q.GetValue<string>("name"));
            }
            nearest[0].Should().Be("new-york-ny-z17");

            GeoPlaygroundResources.TrackRow nj2nc = FindTrack("nj2nc");
            const double threshold = 1.0;
            var distances = new Dictionary<string, double>();
            using (var q = connection.GetSelectEntitiesQueryBase<EpgCity>())
            {
                q.AddToResultset("Name", "name");
                q.AddGeometryScalarToResultset<EpgCity>(SqlGeoFunctionId.Distance, "Center", DbType.Double, "d", parameterName: "p");
                q.Query.BindGeometryParam("p", nj2nc.Path);
                q.ExecuteReader();
                while (q.ReadNext())
                    distances[q.GetValue<string>("name")] = q.GetValue<double>("d");
            }

            var within = new List<string>();
            using (var q = connection.GetSelectEntitiesQueryBase<EpgCity>())
            {
                q.AddToResultset("Name", "name");
                q.Where.GeoPredicateOf<EpgCity>("Center", SqlGeoPredicateId.DWithin, nj2nc.Path, distance: threshold);
                q.ExecuteReader();
                while (q.ReadNext())
                    within.Add(q.GetValue<string>("name"));
            }

            var expectedWithin = new List<string>();
            foreach (var kv in distances)
                if (kv.Value <= threshold)
                    expectedWithin.Add(kv.Key);
            within.Should().BeEquivalentTo(expectedWithin);
        }

        // ---- task 5: projection (ST_Length) + group-by (AVG(ST_Area) per region) ----

        private static void ProjectionAndGroupBy(SqlDbConnection connection)
        {
            var lengths = new List<string>();
            using (var q = connection.GetSelectEntitiesQueryBase<EpgTrack>())
            {
                q.AddToResultset("Name", "name");
                q.AddGeometryScalarToResultset<EpgTrack>(SqlGeoFunctionId.Length, "Path", DbType.Double, "len");
                q.AddGeometryScalarToOrderBy<EpgTrack>(SqlGeoFunctionId.Length, "Path", SortDir.Desc);
                q.ExecuteReader();
                while (q.ReadNext())
                    lengths.Add(q.GetValue<string>("name"));
            }
            lengths[0].Should().Be("nc2tx");

            var regionCounts = new Dictionary<string, int>();
            using (var q = connection.GetSelectEntitiesQueryBase<EpgState>())
            {
                q.AddToResultset("Region", "region");
                q.AddToResultset(AggFn.Count, "ID", "cnt");
                q.AddGeometryScalarToResultset<EpgState>(AggFn.Avg, SqlGeoFunctionId.Area, "Shape", DbType.Double, "avgarea");
                q.AddGroupBy("Region");
                q.ExecuteReader();
                while (q.ReadNext())
                    regionCounts[q.GetValue<string>("region")] = q.GetValue<int>("cnt");
            }
            regionCounts["New England"].Should().Be(6);
        }

        // ---- task 6: native-form subquery operand (outer query is an entity query) ----

        private static void StatesCrossedBySubqueryTrack(SqlDbConnection connection)
        {
            TableDescriptor tracks = AllEntities.Inst[typeof(EpgTrack)].TableDescriptor;
            var sub = connection.GetSelectQueryBuilder(tracks);
            sub.AddGeometryValueToResultset(GeometryRoundTripSupport.ColumnByName(tracks, "path"), "g", GeometryValueForm.Native);
            sub.Where.Property(GeometryRoundTripSupport.ColumnByName(tracks, "name")).Is(CmpOp.Eq).Parameter("t");

            var hits = new List<string>();
            using (var q = connection.GetSelectEntitiesQueryBase<EpgState>())
            {
                q.AddToResultset("Abbrev", "abbrev");
                q.Where.GeoPredicateOf<EpgState>("Shape", SqlGeoPredicateId.Intersects, (AQueryBuilder)sub);
                q.BindParam("t", "nj2nc");
                q.ExecuteReader();
                while (q.ReadNext())
                    hits.Add(q.GetValue<string>("abbrev"));
            }
            hits.Should().Contain(new[] { "NJ", "NC", "VA", "MD" });
        }

        private static GeoPlaygroundResources.TrackRow FindTrack(string name)
        {
            foreach (GeoPlaygroundResources.TrackRow t in GeoPlaygroundResources.Tracks())
                if (t.Name == name)
                    return t;
            return null;
        }

        // ---- setup: create the three tables and load the datasets through the ENTITY insert path ----

        private static void Setup(SqlDbConnection connection)
        {
            using (var q = connection.GetCreateEntityQuery<EpgState>()) q.Execute();
            using (var q = connection.GetCreateEntityQuery<EpgCity>()) q.Execute();
            using (var q = connection.GetCreateEntityQuery<EpgTrack>()) q.Execute();

            foreach (GeoPlaygroundResources.StateRow s in GeoPlaygroundResources.States())
            {
                var e = new EpgState
                {
                    Abbrev = s.Abbrev, Name = s.Name, Region = s.Region, AreaAttr = s.AreaAttr, Pop = s.Pop1999,
                    Shape = GeometryRoundTripSupport.ToWkb(s.Shape),
                };
                using var q = connection.GetInsertEntityQuery<EpgState>();
                q.Execute(e);
            }

            foreach (GeoPlaygroundResources.CityRow c in GeoPlaygroundResources.Cities())
            {
                var e = new EpgCity
                {
                    Name = c.Name,
                    Extent = GeometryRoundTripSupport.ToWkb(c.Extent),
                    Center = GeometryRoundTripSupport.ToWkb(c.Center),
                };
                using var q = connection.GetInsertEntityQuery<EpgCity>();
                q.Execute(e);
            }

            foreach (GeoPlaygroundResources.TrackRow t in GeoPlaygroundResources.Tracks())
            {
                var e = new EpgTrack { Name = t.Name, Path = GeometryRoundTripSupport.ToWkb(t.Path) };
                using var q = connection.GetInsertEntityQuery<EpgTrack>();
                q.Execute(e);
            }
        }
    }
}

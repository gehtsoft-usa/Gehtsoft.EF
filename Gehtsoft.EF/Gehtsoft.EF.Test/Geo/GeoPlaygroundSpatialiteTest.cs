using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// A practical geo "playground" over real US datasets (state polygons, city map-extents, travel tracks;
    /// loaded from embedded resources, see <see cref="GeoPlaygroundResources"/>) on a live SpatiaLite
    /// database. It solves real-world tasks purely through the geo query surface - spatial predicates and
    /// scalar functions - and asserts the confident answers while printing the full results.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeoPlaygroundSpatialiteTest
    {
        private readonly ITestOutputHelper mOut;
        public GeoPlaygroundSpatialiteTest(ITestOutputHelper output) => mOut = output;

        [Entity(Scope = "geo_pg_state", Table = "pg_state")]
        public class PgState
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "abbrev", Size = 2)] public string Abbrev { get; set; }
            [EntityProperty(Field = "name", Size = 64)] public string Name { get; set; }
            [EntityProperty(Field = "region", Size = 32)] public string Region { get; set; }
            [EntityProperty(Field = "area")] public double AreaAttr { get; set; }
            [EntityProperty(Field = "pop")] public long Pop { get; set; }
            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry)] public byte[] Shape { get; set; }
        }

        [Entity(Scope = "geo_pg_city", Table = "pg_city")]
        public class PgCity
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "name", Size = 40)] public string Name { get; set; }
            [GeometryEntityProperty(Field = "extent", Subtype = GeometrySubtype.Polygon)] public byte[] Extent { get; set; }
            [GeometryEntityProperty(Field = "center", Subtype = GeometrySubtype.Point)] public byte[] Center { get; set; }
        }

        [Entity(Scope = "geo_pg_track", Table = "pg_track")]
        public class PgTrack
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
                Setup(connection, out TableDescriptor states, out TableDescriptor cities, out TableDescriptor tracks);

                StatesByArea(connection, states);
                CityInWhichState(connection, states);
                StatesCrossedByTrack(connection, states);
                NearestAndWithinDistance(connection, cities);
                ProjectionAndGroupBy(connection, states, tracks);
                StatesCrossedBySubqueryTrack(connection, states, tracks);
            });
        }

        // resolves a column by its DB field name (the TableDescriptor indexer keys by property name)
        private static TableDescriptor.ColumnInfo Col(TableDescriptor table, string field)
            => GeometryRoundTripSupport.ColumnByName(table, field);

        // ---- practical task 1: states ordered by area (ST_Area), cross-checked vs the AREA attribute ----

        private void StatesByArea(SqlDbConnection connection, TableDescriptor states)
        {
            var b = connection.GetSelectQueryBuilder(states);
            b.AddToResultset(Col(states, "abbrev"), "abbrev");
            b.AddToResultset(Col(states, "area"), "attr");
            b.AddGeometryScalarToResultset(SqlGeoFunctionId.Area, Col(states, "shape"), DbType.Double, "garea");
            b.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Area, Col(states, "shape"), SortDir.Desc);

            var order = new List<string>();
            var gareas = new List<double>();
            mOut.WriteLine("== states by area (desc): abbrev  ST_Area  attrArea ==");
            using (var q = connection.GetQuery(b))
            {
                q.ExecuteReader();
                while (q.ReadNext())
                {
                    string abbrev = q.GetValue<string>("abbrev");
                    double garea = q.GetValue<double>("garea");
                    double attr = q.GetValue<double>("attr");
                    order.Add(abbrev);
                    gareas.Add(garea);
                    mOut.WriteLine($"  {abbrev}  {garea:F3}  {attr:F3}");
                }
            }

            order.Should().HaveCount(22);
            // ST_Area sort is genuinely descending
            for (int i = 1; i < gareas.Count; i++)
                gareas[i].Should().BeLessThanOrEqualTo(gareas[i - 1]);
            // the largest-by-geometry states are the big eastern ones (NY/NC/FL/GA/ME), never DC/RI/DE
            order[0].Should().BeOneOf("NY", "NC", "FL", "GA", "ME", "VA", "PA");
            order[order.Count - 1].Should().BeOneOf("DC", "RI", "DE");
        }

        // ---- practical task 2: which state is each city in (state CONTAINS the city centre) ----

        private void CityInWhichState(SqlDbConnection connection, TableDescriptor states)
        {
            var found = new Dictionary<string, string>();
            mOut.WriteLine("== city -> containing state(s) ==");

            foreach (GeoPlaygroundResources.CityRow city in GeoPlaygroundResources.Cities())
            {
                var b = connection.GetSelectQueryBuilder(states);
                b.AddToResultset(Col(states, "abbrev"), "abbrev");
                b.Where.GeoPredicate(SqlGeoPredicateId.Contains, Col(states, "shape"), "p");

                var hits = new List<string>();
                using (var q = connection.GetQuery(b))
                {
                    q.BindGeometryParam("p", (Geometry)city.Center);
                    q.ExecuteReader();
                    while (q.ReadNext())
                        hits.Add(q.GetValue<string>("abbrev"));
                }
                found[city.Name] = string.Join(",", hits);
                mOut.WriteLine($"  {city.Name,-24} -> [{found[city.Name]}]");
            }

            // the unambiguous inland/coastal NC & VA cities land squarely in their state
            found["charlotte-nc-z17"].Should().Contain("NC");
            found["wilmington-nc-z17"].Should().Contain("NC");
            found["emerald-isle-nc-z17"].Should().Contain("NC");
            found["richmond-va-z17"].Should().Contain("VA");
        }

        // ---- practical task 3: which states does each track cross (state INTERSECTS the track) ----

        private void StatesCrossedByTrack(SqlDbConnection connection, TableDescriptor states)
        {
            var crossed = new Dictionary<string, List<string>>();
            mOut.WriteLine("== track -> crossed states ==");

            foreach (GeoPlaygroundResources.TrackRow track in GeoPlaygroundResources.Tracks())
            {
                var b = connection.GetSelectQueryBuilder(states);
                b.AddToResultset(Col(states, "abbrev"), "abbrev");
                b.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Area, Col(states, "shape"), SortDir.Desc);
                b.Where.GeoPredicate(SqlGeoPredicateId.Intersects, Col(states, "shape"), "p");

                var hits = new List<string>();
                using (var q = connection.GetQuery(b))
                {
                    q.BindGeometryParam("p", (Geometry)track.Path);
                    q.ExecuteReader();
                    while (q.ReadNext())
                        hits.Add(q.GetValue<string>("abbrev"));
                }
                crossed[track.Name] = hits;
                mOut.WriteLine($"  {track.Name,-8} -> [{string.Join(",", hits)}]");
            }

            crossed["nc2tx"].Should().Contain("NC");                       // starts in North Carolina
            crossed["nj2nc"].Should().Contain(new[] { "NJ", "NC" });        // New Jersey down to North Carolina
            crossed["nj2oh"].Should().Contain(new[] { "NJ", "PA", "OH" });  // New Jersey across PA to Ohio
        }

        // ---- practical task 4: nearest city (order-by-distance top-N) + within-distance (DWithin) ----

        private void NearestAndWithinDistance(SqlDbConnection connection, TableDescriptor cities)
        {
            // 4a) the three nearest cities to a probe point near New York City, by ST_Distance
            var probe = new Point(-74.0, 40.7) { SRID = GeoPlaygroundResources.Srid };
            var nearest = new List<string>();
            mOut.WriteLine("== 3 nearest cities to (-74.0, 40.7) ==");
            var b = connection.GetSelectQueryBuilder(cities);
            b.AddToResultset(Col(cities, "name"), "name");
            b.AddGeometryScalarToResultset(SqlGeoFunctionId.Distance, Col(cities, "center"), DbType.Double, "d", parameterName: "p");
            b.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Distance, Col(cities, "center"), SortDir.Asc, parameterName: "p");
            b.Limit = 3;
            using (var q = connection.GetQuery(b))
            {
                q.BindGeometryParam("p", probe);
                q.ExecuteReader();
                while (q.ReadNext())
                {
                    nearest.Add(q.GetValue<string>("name"));
                    mOut.WriteLine($"  {q.GetValue<string>("name"),-24} dist={q.GetValue<double>("d"):F3}");
                }
            }
            nearest[0].Should().Be("new-york-ny-z17");

            // 4b) DWithin cross-checked against the projected distance of each city to the nj2nc track:
            // the set returned by the DWithin predicate must be exactly the cities whose measured distance
            // is within the threshold. First project every city's distance to the track...
            GeoPlaygroundResources.TrackRow nj2nc = FindTrack("nj2nc");
            const double threshold = 1.0;   // degrees (~110 km)
            var distances = new Dictionary<string, double>();
            mOut.WriteLine("== city distance to the nj2nc track ==");
            var b2 = connection.GetSelectQueryBuilder(cities);
            b2.AddToResultset(Col(cities, "name"), "name");
            b2.AddGeometryScalarToResultset(SqlGeoFunctionId.Distance, Col(cities, "center"), DbType.Double, "d", parameterName: "p");
            using (var q = connection.GetQuery(b2))
            {
                q.BindGeometryParam("p", (Geometry)nj2nc.Path);
                q.ExecuteReader();
                while (q.ReadNext())
                {
                    distances[q.GetValue<string>("name")] = q.GetValue<double>("d");
                    mOut.WriteLine($"  {q.GetValue<string>("name"),-24} dist={q.GetValue<double>("d"):F3}");
                }
            }

            // ...then run the DWithin predicate and check the two agree.
            var within = new List<string>();
            var b3 = connection.GetSelectQueryBuilder(cities);
            b3.AddToResultset(Col(cities, "name"), "name");
            b3.Where.GeoPredicate(SqlGeoPredicateId.DWithin, Col(cities, "center"), "p", distance: threshold);
            using (var q = connection.GetQuery(b3))
            {
                q.BindGeometryParam("p", (Geometry)nj2nc.Path);
                q.ExecuteReader();
                while (q.ReadNext())
                    within.Add(q.GetValue<string>("name"));
            }
            mOut.WriteLine($"== cities within {threshold}deg of nj2nc -> [{string.Join(",", within)}] ==");

            var expectedWithin = new List<string>();
            foreach (var kv in distances)
                if (kv.Value <= threshold)
                    expectedWithin.Add(kv.Key);
            within.Should().BeEquivalentTo(expectedWithin);   // DWithin agrees with the measured distances
        }

        // ---- practical task 5: projection (ST_Length) + group-by (AVG(ST_Area) per region) ----

        private void ProjectionAndGroupBy(SqlDbConnection connection, TableDescriptor states, TableDescriptor tracks)
        {
            // 5a) each track's length, longest first
            var lengths = new List<string>();
            mOut.WriteLine("== track lengths (ST_Length, desc) ==");
            var b = connection.GetSelectQueryBuilder(tracks);
            b.AddToResultset(Col(tracks, "name"), "name");
            b.AddGeometryScalarToResultset(SqlGeoFunctionId.Length, Col(tracks, "path"), DbType.Double, "len");
            b.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Length, Col(tracks, "path"), SortDir.Desc);
            using (var q = connection.GetQuery(b))
            {
                q.ExecuteReader();
                while (q.ReadNext())
                {
                    lengths.Add(q.GetValue<string>("name"));
                    mOut.WriteLine($"  {q.GetValue<string>("name"),-8} len={q.GetValue<double>("len"):F3}");
                }
            }
            lengths[0].Should().Be("nc2tx");   // NC->TX is the widest-spanning track

            // 5b) states grouped by region: count + average ST_Area
            var regionCounts = new Dictionary<string, int>();
            mOut.WriteLine("== states per region: count, AVG(ST_Area) ==");
            var b2 = connection.GetSelectQueryBuilder(states);
            b2.AddToResultset(Col(states, "region"), "region");
            b2.AddToResultset(AggFn.Count, "cnt");
            b2.AddGeometryScalarToResultset(AggFn.Avg, SqlGeoFunctionId.Area, Col(states, "shape"), DbType.Double, "avgarea");
            b2.AddGroupBy(Col(states, "region"));
            using (var q = connection.GetQuery(b2))
            {
                q.ExecuteReader();
                while (q.ReadNext())
                {
                    string region = q.GetValue<string>("region");
                    int cnt = q.GetValue<int>("cnt");
                    regionCounts[region] = cnt;
                    mOut.WriteLine($"  {region,-14} count={cnt}  avgArea={q.GetValue<double>("avgarea"):F3}");
                }
            }
            // the six New England states (VT, RI, NH, ME, MA, CT) group together
            regionCounts["New England"].Should().Be(6);
        }

        // ---- practical task 6: the NEW native-form subquery operand, on real data ----
        // states crossed by the track whose name=@t, with the track geometry supplied by a SUBQUERY
        // (projected in its native form) rather than a bound parameter.

        private void StatesCrossedBySubqueryTrack(SqlDbConnection connection, TableDescriptor states, TableDescriptor tracks)
        {
            var sub = connection.GetSelectQueryBuilder(tracks);
            sub.AddGeometryValueToResultset(Col(tracks, "path"), "g", GeometryValueForm.Native);
            sub.Where.Property(Col(tracks, "name")).Is(CmpOp.Eq).Parameter("t");

            var b = connection.GetSelectQueryBuilder(states);
            b.AddToResultset(Col(states, "abbrev"), "abbrev");
            b.Where.GeoPredicate(SqlGeoPredicateId.Intersects, Col(states, "shape"), sub);

            var hits = new List<string>();
            using (var q = connection.GetQuery(b))
            {
                q.BindParam("t", "nj2nc");
                q.ExecuteReader();
                while (q.ReadNext())
                    hits.Add(q.GetValue<string>("abbrev"));
            }
            mOut.WriteLine($"== states crossed by nj2nc (operand from SUBQUERY) -> [{string.Join(",", hits)}] ==");
            // same corridor the bound-parameter form produced
            hits.Should().Contain(new[] { "NJ", "NC", "VA", "MD" });
        }

        private static GeoPlaygroundResources.TrackRow FindTrack(string name)
        {
            foreach (GeoPlaygroundResources.TrackRow t in GeoPlaygroundResources.Tracks())
                if (t.Name == name)
                    return t;
            return null;
        }

        // ---- setup: create the three tables and load the minimized datasets ----

        private static void Setup(SqlDbConnection connection, out TableDescriptor states, out TableDescriptor cities, out TableDescriptor tracks)
        {
            using (var q = connection.GetCreateEntityQuery<PgState>()) q.Execute();
            using (var q = connection.GetCreateEntityQuery<PgCity>()) q.Execute();
            using (var q = connection.GetCreateEntityQuery<PgTrack>()) q.Execute();

            states = AllEntities.Inst[typeof(PgState)].TableDescriptor;
            cities = AllEntities.Inst[typeof(PgCity)].TableDescriptor;
            tracks = AllEntities.Inst[typeof(PgTrack)].TableDescriptor;

            foreach (GeoPlaygroundResources.StateRow s in GeoPlaygroundResources.States())
            {
                var b = connection.GetInsertQueryBuilder(states);
                b.ReturnAutoincrement = false;
                WrapGeo(connection, b, "shape");
                using var q = connection.GetQuery(b);
                q.BindParam("abbrev", s.Abbrev);
                q.BindParam("name", s.Name);
                q.BindParam("region", s.Region);
                q.BindParam("area", s.AreaAttr);
                q.BindParam("pop", s.Pop1999);
                q.BindGeometryParam("shape", s.Shape);
                q.ExecuteNoData();
            }

            foreach (GeoPlaygroundResources.CityRow c in GeoPlaygroundResources.Cities())
            {
                var b = connection.GetInsertQueryBuilder(cities);
                b.ReturnAutoincrement = false;
                WrapGeo(connection, b, "extent", "center");
                using var q = connection.GetQuery(b);
                q.BindParam("name", c.Name);
                q.BindGeometryParam("extent", c.Extent);
                q.BindGeometryParam("center", c.Center);
                q.ExecuteNoData();
            }

            foreach (GeoPlaygroundResources.TrackRow t in GeoPlaygroundResources.Tracks())
            {
                var b = connection.GetInsertQueryBuilder(tracks);
                b.ReturnAutoincrement = false;
                WrapGeo(connection, b, "path");
                using var q = connection.GetQuery(b);
                q.BindParam("name", t.Name);
                q.BindGeometryParam("path", t.Path);
                q.ExecuteNoData();
            }
        }

        // Wraps each named geometry column's bound WKB parameter in the dialect's constructor function.
        private static void WrapGeo(SqlDbConnection connection, InsertQueryBuilder builder, params string[] columns)
        {
            var specifics = connection.GetLanguageSpecifics();
            var exprs = new (string, string)[columns.Length];
            for (int i = 0; i < columns.Length; i++)
                exprs[i] = (columns[i], specifics.GeometryFunction(new GeoFunctionRequest(
                    SqlGeoFunctionId.FromWkb, parameter: InsertQueryBuilder.ParameterToken, srid: GeoPlaygroundResources.Srid)));
            builder.SetColumnValueExpressions(exprs);
        }
    }
}

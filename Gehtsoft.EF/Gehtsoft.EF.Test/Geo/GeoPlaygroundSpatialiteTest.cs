using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
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
                Setup(connection, out TableDescriptor states, out _, out _);

                StatesByArea(connection, states);
                CityInWhichState(connection, states);
                StatesCrossedByTrack(connection, states);
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

using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.Linq
{
    /// <summary>
    /// Acceptance (black-box) tests for reading a dynamic property from the entity LINQ surface
    /// (Phase 4a): <c>e.DynamicProperties.Get&lt;T&gt;("name")</c> in WHERE, in a Select projection
    /// and inside an aggregate, over every value type on every driver. The value is filtered /
    /// projected / aggregated through the reused Phase-3 side-table join; bool (0/1) and DateTime
    /// (UTC ticks) round-trip through the encode-on-compare and decode-on-read paths.
    /// </summary>
    public class DynamicPropertiesLinqTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;
        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);
        public DynamicPropertiesLinqTest(SqlConnectionFixtureBase fixture) { mFixture = fixture; }

        [Entity(Scope = "dp_linq", Table = "dp_linq_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static readonly DateTime When1 = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        private static readonly DateTime When2 = new DateTime(2022, 9, 10, 11, 12, 13, DateTimeKind.Utc);
        private static readonly DateTime Between = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static void Insert(SqlDbConnection c, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using var q = c.GetInsertEntityQuery<Owner>();
            q.Execute(e);
        }

        // a: full set (color red, size 10, big 100, weight 1.5, flag true, when1)
        // b: full set (color blue, size 30, big 300, weight 2.5, flag false, when2)
        // c: no dynamic properties (every property reads as SQL NULL / absent)
        private static void Seed(SqlDbConnection c)
        {
            Insert(c, "a", b => { b.Set("color", "red"); b.Set("size", 10); b.Set("big", 100L); b.Set("weight", 1.5); b.Set("flag", true); b.Set("when", When1); });
            Insert(c, "b", b => { b.Set("color", "blue"); b.Set("size", 30); b.Set("big", 300L); b.Set("weight", 2.5); b.Set("flag", false); b.Set("when", When2); });
            Insert(c, "c", b => { });
        }

        private void Run(string connectionName, Action<SqlDbConnection> body)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
            try { Seed(c); body(c); }
            finally { using var q = c.GetDropEntityQuery<Owner>(); q.Execute(); }
        }

        private static HashSet<string> Names(IEnumerable<Owner> owners)
        {
            var set = new HashSet<string>();
            foreach (var o in owners)
                set.Add(o.Name);
            return set;
        }

        // ---- WHERE: string (the primary EAV filter) ------------------------------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_String_Equal(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<string>("color") == "red").ToList())
                    .Should().BeEquivalentTo(new[] { "a" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_String_NotEqual(string connectionName)
            // c is absent (NULL) so <> 'red' drops it too - SQL three-valued logic
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<string>("color") != "red").ToList())
                    .Should().BeEquivalentTo(new[] { "b" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_String_Like(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => SqlFunction.Like(e.DynamicProperties.Get<string>("color"), "bl%")).ToList())
                    .Should().BeEquivalentTo(new[] { "b" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_String_StartsWith(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<string>("color").StartsWith("re")).ToList())
                    .Should().BeEquivalentTo(new[] { "a" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_String_Length(string connectionName)
            // "blue".Length == 4 > 3 ; "red".Length == 3 is not
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<string>("color").Length > 3).ToList())
                    .Should().BeEquivalentTo(new[] { "b" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_String_IsNull(string connectionName)
            => Run(connectionName, c =>
            {
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<string>("color") == null).ToList())
                    .Should().BeEquivalentTo(new[] { "c" });
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<string>("color") != null).ToList())
                    .Should().BeEquivalentTo(new[] { "a", "b" });
            });

        // ---- WHERE: numeric ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Int(string connectionName)
            => Run(connectionName, c =>
            {
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<int>("size") > 20).ToList())
                    .Should().BeEquivalentTo(new[] { "b" });
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<int>("size") == 10).ToList())
                    .Should().BeEquivalentTo(new[] { "a" });
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Long(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<long>("big") >= 300L).ToList())
                    .Should().BeEquivalentTo(new[] { "b" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Double(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<double>("weight") < 2.0).ToList())
                    .Should().BeEquivalentTo(new[] { "a" }));

        // ---- WHERE: bool / DateTime (encode-on-compare) --------------------------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Bool(string connectionName)
            => Run(connectionName, c =>
            {
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<bool>("flag") == true).ToList())
                    .Should().BeEquivalentTo(new[] { "a" });
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<bool>("flag") == false).ToList())
                    .Should().BeEquivalentTo(new[] { "b" });
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_DateTime(string connectionName)
            => Run(connectionName, c =>
            {
                // local (captured) values, not static fields: the compiler reads a static-field
                // reference as a MemberExpression with a null .Expression and cannot handle it
                var between = Between;
                var when1 = When1;
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<DateTime>("when") > between).ToList())
                    .Should().BeEquivalentTo(new[] { "b" });
                Names(c.GetCollectionOf<Owner>().Where(e => e.DynamicProperties.Get<DateTime>("when") == when1).ToList())
                    .Should().BeEquivalentTo(new[] { "a" });
            });

        // ---- Projection (decode-on-read); nullable T covers the absent 'c' -------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_String(string connectionName)
            => Run(connectionName, c =>
            {
                var map = new Dictionary<string, string>();
                foreach (var row in c.GetCollectionOf<Owner>().Select(e => new { e.Name, Value = e.DynamicProperties.Get<string>("color") }).ToList())
                    map[row.Name] = row.Value;
                map["a"].Should().Be("red");
                map["b"].Should().Be("blue");
                map["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Int(string connectionName)
            => Run(connectionName, c =>
            {
                var map = new Dictionary<string, int?>();
                foreach (var row in c.GetCollectionOf<Owner>().Select(e => new { e.Name, Value = e.DynamicProperties.Get<int?>("size") }).ToList())
                    map[row.Name] = row.Value;
                map["a"].Should().Be(10);
                map["b"].Should().Be(30);
                map["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Long(string connectionName)
            => Run(connectionName, c =>
            {
                var map = new Dictionary<string, long?>();
                foreach (var row in c.GetCollectionOf<Owner>().Select(e => new { e.Name, Value = e.DynamicProperties.Get<long?>("big") }).ToList())
                    map[row.Name] = row.Value;
                map["a"].Should().Be(100L);
                map["b"].Should().Be(300L);
                map["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Double(string connectionName)
            => Run(connectionName, c =>
            {
                var map = new Dictionary<string, double?>();
                foreach (var row in c.GetCollectionOf<Owner>().Select(e => new { e.Name, Value = e.DynamicProperties.Get<double?>("weight") }).ToList())
                    map[row.Name] = row.Value;
                map["a"].Should().BeApproximately(1.5, 1e-9);
                map["b"].Should().BeApproximately(2.5, 1e-9);
                map["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Bool(string connectionName)
            => Run(connectionName, c =>
            {
                var map = new Dictionary<string, bool?>();
                foreach (var row in c.GetCollectionOf<Owner>().Select(e => new { e.Name, Value = e.DynamicProperties.Get<bool?>("flag") }).ToList())
                    map[row.Name] = row.Value;
                map["a"].Should().Be(true);
                map["b"].Should().Be(false);
                map["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_DateTime(string connectionName)
            => Run(connectionName, c =>
            {
                var map = new Dictionary<string, DateTime?>();
                foreach (var row in c.GetCollectionOf<Owner>().Select(e => new { e.Name, Value = e.DynamicProperties.Get<DateTime?>("when") }).ToList())
                    map[row.Name] = row.Value;
                map["a"].Should().Be(When1);
                map["b"].Should().Be(When2);
                map["c"].Should().BeNull();
            });

        // ---- Aggregates (NULL rows ignored by SQL aggregation) -------------------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregate_Numeric(string connectionName)
            => Run(connectionName, c =>
            {
                c.GetCollectionOf<Owner>().Select(e => SqlFunction.Max(e.DynamicProperties.Get<int>("size"))).First().Should().Be(30);
                c.GetCollectionOf<Owner>().Select(e => SqlFunction.Min(e.DynamicProperties.Get<int>("size"))).First().Should().Be(10);
                c.GetCollectionOf<Owner>().Select(e => SqlFunction.Sum(e.DynamicProperties.Get<double>("weight"))).First().Should().BeApproximately(4.0, 1e-9);
                c.GetCollectionOf<Owner>().Select(e => SqlFunction.Avg(e.DynamicProperties.Get<double>("weight"))).First().Should().BeApproximately(2.0, 1e-9);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregate_DateTime_MinMax(string connectionName)
            => Run(connectionName, c =>
            {
                c.GetCollectionOf<Owner>().Select(e => SqlFunction.Max(e.DynamicProperties.Get<DateTime>("when"))).First().Should().Be(When2);
                c.GetCollectionOf<Owner>().Select(e => SqlFunction.Min(e.DynamicProperties.Get<DateTime>("when"))).First().Should().Be(When1);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregate_Count(string connectionName)
            => Run(connectionName, c =>
                c.GetCollectionOf<Owner>().Count(e => e.DynamicProperties.Get<int>("size") > 5).Should().Be(2));

        // ---- ORDER BY / GROUP BY by a dynamic property (Phase 4b) ---------------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OrderBy_String(string connectionName)
            // "blue" (b) < "red" (a); c (absent) filtered out to keep NULL-ordering driver-neutral
            => Run(connectionName, c =>
            {
                var names = new List<string>();
                foreach (var o in c.GetCollectionOf<Owner>()
                            .Where(e => e.DynamicProperties.Get<string>("color") != null)
                            .OrderBy(e => e.DynamicProperties.Get<string>("color")).ToList())
                    names.Add(o.Name);
                names.Should().Equal("b", "a");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OrderBy_Int(string connectionName)
            // size 10 (a) < 30 (b)
            => Run(connectionName, c =>
            {
                var names = new List<string>();
                foreach (var o in c.GetCollectionOf<Owner>()
                            .Where(e => e.DynamicProperties.Get<string>("color") != null)
                            .OrderBy(e => e.DynamicProperties.Get<int>("size")).ToList())
                    names.Add(o.Name);
                names.Should().Equal("a", "b");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void GroupBy_Count(string connectionName)
            => Run(connectionName, c =>
            {
                Insert(c, "d", b => { b.Set("color", "red"); b.Set("size", 50); });

                var map = new Dictionary<string, int>();
                foreach (var row in c.GetCollectionOf<Owner>()
                            .Where(e => e.DynamicProperties.Get<string>("color") != null)
                            .GroupBy(e => e.DynamicProperties.Get<string>("color"))
                            .Select(g => new { Color = g.Key, Cnt = g.Count() }).ToList())
                    map[row.Color] = row.Cnt;

                map["red"].Should().Be(2);   // a, d
                map["blue"].Should().Be(1);  // b
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void GroupBy_Aggregate_Max(string connectionName)
            => Run(connectionName, c =>
            {
                Insert(c, "d", b => { b.Set("color", "red"); b.Set("size", 50); });

                var map = new Dictionary<string, int>();
                foreach (var row in c.GetCollectionOf<Owner>()
                            .Where(e => e.DynamicProperties.Get<string>("color") != null)
                            .GroupBy(e => e.DynamicProperties.Get<string>("color"))
                            .Select(g => new { Color = g.Key, MaxSize = g.Max(v => v.DynamicProperties.Get<int>("size")) }).ToList())
                    map[row.Color] = row.MaxSize;

                map["red"].Should().Be(50);  // max(10, 50)
                map["blue"].Should().Be(30); // b
            });

        // ---- Whole-entity select with opt-in dynamic-property preload -----------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WholeEntity_OptOut_BagIsNull(string connectionName)
            // opt out: the bag is not loaded on a whole-entity select
            => Run(connectionName, c =>
            {
                foreach (var o in c.GetCollectionOf<Owner>(preloadDynamicProperties: false).ToList())
                    o.DynamicProperties.Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WholeEntity_Preload_ToList(string connectionName)
            // preload is the default (no argument)
            => Run(connectionName, c =>
            {
                var byName = new Dictionary<string, Owner>();
                foreach (var o in c.GetCollectionOf<Owner>().ToList())
                    byName[o.Name] = o;

                byName["a"].DynamicProperties.Should().NotBeNull();
                byName["a"].DynamicProperties.IsNew.Should().BeFalse();
                byName["a"].DynamicProperties.Get<string>("color").Should().Be("red");
                byName["a"].DynamicProperties.Get<int>("size").Should().Be(10);
                byName["a"].DynamicProperties.Get<long>("big").Should().Be(100L);
                byName["a"].DynamicProperties.Get<double>("weight").Should().Be(1.5);
                byName["a"].DynamicProperties.Get<bool>("flag").Should().BeTrue();
                byName["a"].DynamicProperties.Get<DateTime>("when").Should().Be(When1);

                byName["b"].DynamicProperties.Get<string>("color").Should().Be("blue");

                // an owner with no dynamic properties gets an attached, empty (non-null) bag
                byName["c"].DynamicProperties.Should().NotBeNull();
                byName["c"].DynamicProperties.Count.Should().Be(0);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WholeEntity_Preload_First(string connectionName)
            => Run(connectionName, c =>
            {
                var a = c.GetCollectionOf<Owner>().Where(e => e.Name == "a").First();
                a.DynamicProperties.Should().NotBeNull();
                a.DynamicProperties.Get<string>("color").Should().Be("red");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WholeEntity_Preload_FilteredByDynamicProperty(string connectionName)
            // filter BY a dynamic property AND preload the bag on the returned entities
            => Run(connectionName, c =>
            {
                var list = c.GetCollectionOf<Owner>()
                            .Where(e => e.DynamicProperties.Get<string>("color") == "red").ToList();
                list.Should().HaveCount(1);
                list[0].Name.Should().Be("a");
                list[0].DynamicProperties.Get<int>("size").Should().Be(10);
            });

        // ---- Regression: ordinary (non-dynamic) LINQ still works (shared compiler blast radius)

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Regression_PlainQuery(string connectionName)
            => Run(connectionName, c =>
            {
                c.GetCollectionOf<Owner>().Count().Should().Be(3);
                c.GetCollectionOf<Owner>().Count(e => e.Name == "a").Should().Be(1);
                Names(c.GetCollectionOf<Owner>().Where(e => e.Name != "c").ToList())
                    .Should().BeEquivalentTo(new[] { "a", "b" });
            });
    }
}

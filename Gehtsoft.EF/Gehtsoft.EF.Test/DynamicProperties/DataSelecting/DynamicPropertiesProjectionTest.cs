using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataSelecting
{
    /// <summary>
    /// Acceptance (black-box) tests for projecting a dynamic property into a custom entity query on
    /// every driver: the joined value comes back decoded to its declared type (DateTime exact, bool,
    /// numbers), an absent property reads as null, and aggregates return the expected values.
    /// </summary>
    public class DynamicPropertiesProjectionTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;
        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);
        public DynamicPropertiesProjectionTest(SqlConnectionFixtureBase fixture) { mFixture = fixture; }

        [Entity(Scope = "dp_proj", Table = "dp_proj_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static readonly DateTime When1 = new DateTime(2021, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        private static readonly DateTime When2 = new DateTime(2022, 9, 10, 11, 12, 13, DateTimeKind.Utc);

        private static void Insert(SqlDbConnection c, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = c.GetInsertEntityQuery<Owner>()) q.Execute(e);
        }

        private static void Seed(SqlDbConnection c)
        {
            Insert(c, "a", b => { b.Set("color", "red"); b.Set("size", 10); b.Set("flag", true); b.Set("weight", 1.5); b.Set("when", When1); });
            Insert(c, "b", b => { b.Set("color", "blue"); b.Set("size", 30); b.Set("flag", false); b.Set("weight", 2.5); b.Set("when", When2); });
            Insert(c, "c", b => { /* no dynamic properties */ });
        }

        private void Run(string connectionName, Action<SqlDbConnection> body)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
            try { Seed(c); body(c); }
            finally { using (var q = c.GetDropEntityQuery<Owner>()) q.Execute(); }
        }

        // Projects Name + one dynamic property and returns a name -> value map.
        private static Dictionary<string, object> Project(SqlDbConnection c, string property, DynamicPropertyValueType type)
        {
            using var q = c.GetSelectEntitiesQueryBase<Owner>();
            q.AddToResultset(typeof(Owner), nameof(Owner.Name), "name");
            q.AddDynamicPropertyToResultset<Owner>(property, type, "value");

            var result = new Dictionary<string, object>();
            foreach (var row in q.ReadAllDynamic())
            {
                var dict = (IDictionary<string, object>)row;
                result[(string)dict["name"]] = dict["value"];
            }
            return result;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_String(string connectionName)
            => Run(connectionName, c =>
            {
                var v = Project(c, "color", DynamicPropertyValueType.String);
                v["a"].Should().Be("red");
                v["b"].Should().Be("blue");
                v["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Integer(string connectionName)
            => Run(connectionName, c =>
            {
                var v = Project(c, "size", DynamicPropertyValueType.Integer);
                v["a"].Should().Be(10);
                v["b"].Should().Be(30);
                v["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Boolean(string connectionName)
            => Run(connectionName, c =>
            {
                var v = Project(c, "flag", DynamicPropertyValueType.Boolean);
                v["a"].Should().Be(true);
                v["b"].Should().Be(false);
                v["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Real(string connectionName)
            => Run(connectionName, c =>
            {
                var v = Project(c, "weight", DynamicPropertyValueType.Real);
                v["a"].Should().Be(1.5);
                v["b"].Should().Be(2.5);
                v["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_Long(string connectionName)
            => Run(connectionName, c =>
            {
                // a 64-bit value beyond the Int32 range, projected as Long
                const long huge = 5_000_000_000L;
                Insert(c, "big", b => b.Set("huge", huge));

                using var q = c.GetSelectEntitiesQueryBase<Owner>();
                q.AddToResultset(typeof(Owner), nameof(Owner.Name), "name");
                q.AddDynamicPropertyToResultset<Owner>("huge", DynamicPropertyValueType.Long, "value");

                object value = null;
                foreach (var row in q.ReadAllDynamic())
                {
                    var dict = (IDictionary<string, object>)row;
                    if ((string)dict["name"] == "big")
                        value = dict["value"];
                }

                value.Should().Be(huge);   // decoded back to a long, not truncated to int
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_DateTime_Exact(string connectionName)
            => Run(connectionName, c =>
            {
                var v = Project(c, "when", DynamicPropertyValueType.DateTime);
                v["a"].Should().Be(When1);
                v["b"].Should().Be(When2);
                v["c"].Should().BeNull();
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Project_MultipleProperties(string connectionName)
            => Run(connectionName, c =>
            {
                // Aliases are prefixed so they cannot collide with reserved words on any driver
                // (e.g. SIZE is reserved on Oracle, WHEN on most).
                using var q = c.GetSelectEntitiesQueryBase<Owner>();
                q.AddToResultset(typeof(Owner), nameof(Owner.Name), "dp_name");
                q.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "dp_color");
                q.AddDynamicPropertyToResultset<Owner>("size", DynamicPropertyValueType.Integer, "dp_size");
                q.AddDynamicPropertyToResultset<Owner>("flag", DynamicPropertyValueType.Boolean, "dp_flag");
                q.AddDynamicPropertyToResultset<Owner>("weight", DynamicPropertyValueType.Real, "dp_weight");
                q.AddDynamicPropertyToResultset<Owner>("when", DynamicPropertyValueType.DateTime, "dp_when");

                var rows = new Dictionary<string, IDictionary<string, object>>();
                foreach (var row in q.ReadAllDynamic())
                {
                    var dict = (IDictionary<string, object>)row;
                    rows[(string)dict["dp_name"]] = dict;
                }

                rows.Should().HaveCount(3);

                rows["a"]["dp_color"].Should().Be("red");
                rows["a"]["dp_size"].Should().Be(10);
                rows["a"]["dp_flag"].Should().Be(true);
                rows["a"]["dp_weight"].Should().Be(1.5);
                rows["a"]["dp_when"].Should().Be(When1);

                rows["b"]["dp_color"].Should().Be("blue");
                rows["b"]["dp_size"].Should().Be(30);
                rows["b"]["dp_flag"].Should().Be(false);
                rows["b"]["dp_weight"].Should().Be(2.5);
                rows["b"]["dp_when"].Should().Be(When2);

                // owner "c" has no dynamic properties - every projected value is null
                rows["c"]["dp_color"].Should().BeNull();
                rows["c"]["dp_size"].Should().BeNull();
                rows["c"]["dp_flag"].Should().BeNull();
                rows["c"]["dp_weight"].Should().BeNull();
                rows["c"]["dp_when"].Should().BeNull();
            });

        // Names matching a filter via the optimized (projected -> direct join) path.
        private static List<string> ProjectedFilterNames(SqlDbConnection c, string property, DynamicPropertyValueType type, Action<DynamicPropertyConditionBuilder> filter)
        {
            using var q = c.GetSelectEntitiesQueryBase<Owner>();
            q.AddToResultset(typeof(Owner), nameof(Owner.Name), "dp_name");
            q.AddDynamicPropertyToResultset<Owner>(property, type, "dp_value");
            filter(q.Where.DynamicPropertyOf<Owner>(property));

            var names = new List<string>();
            foreach (var row in q.ReadAllDynamic())
                names.Add((string)((IDictionary<string, object>)row)["dp_name"]);
            return names;
        }

        // Names matching a filter via the unoptimized (owner IN (SELECT ...)) path.
        private static List<string> SubqueryFilterNames(SqlDbConnection c, string property, Action<DynamicPropertyConditionBuilder> filter)
        {
            using var q = c.GetSelectEntitiesQuery<Owner>();
            filter(q.Where.DynamicPropertyOf<Owner>(property));

            var names = new List<string>();
            foreach (var o in q.ReadAll<Owner>())
                names.Add(o.Name);
            return names;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Optimized_Eq_MatchesSubquery(string connectionName)
            => Run(connectionName, c =>
            {
                ProjectedFilterNames(c, "color", DynamicPropertyValueType.String, f => f.Eq("red"))
                    .Should().BeEquivalentTo(new[] { "a" });
                // the optimized (direct) form returns exactly what the sub-query form returns
                ProjectedFilterNames(c, "color", DynamicPropertyValueType.String, f => f.Eq("red"))
                    .Should().BeEquivalentTo(SubqueryFilterNames(c, "color", f => f.Eq("red")));
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Optimized_Range_MatchesSubquery(string connectionName)
            => Run(connectionName, c =>
            {
                ProjectedFilterNames(c, "size", DynamicPropertyValueType.Integer, f => f.Ge(30))
                    .Should().BeEquivalentTo(new[] { "b" });
                ProjectedFilterNames(c, "size", DynamicPropertyValueType.Integer, f => f.Ge(30))
                    .Should().BeEquivalentTo(SubqueryFilterNames(c, "size", f => f.Ge(30)));
            });

        // Names ordered by a projected dynamic property. The WHERE (optimized) drops the owner that
        // lacks the property, so the assertion does not depend on driver NULL-ordering.
        private static List<string> OrderedNames(SqlDbConnection c, SortDir direction)
        {
            using var q = c.GetSelectEntitiesQueryBase<Owner>();
            q.AddToResultset(typeof(Owner), nameof(Owner.Name), "dp_name");
            q.AddDynamicPropertyToResultset<Owner>("size", DynamicPropertyValueType.Integer, "dp_size");
            q.Where.DynamicPropertyOf<Owner>("size").Ge(1);
            q.AddDynamicPropertyToOrderBy<Owner>("size", DynamicPropertyValueType.Integer, direction);

            var names = new List<string>();
            foreach (var row in q.ReadAllDynamic())
                names.Add((string)((IDictionary<string, object>)row)["dp_name"]);
            return names;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OrderBy_Integer_Ascending(string connectionName)
            => Run(connectionName, c => OrderedNames(c, SortDir.Asc).Should().Equal("a", "b")); // 10, 30

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OrderBy_Integer_Descending(string connectionName)
            => Run(connectionName, c => OrderedNames(c, SortDir.Desc).Should().Equal("b", "a")); // 30, 10

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void GroupBy_CountPerValue(string connectionName)
            => Run(connectionName, c =>
            {
                // seed adds a=red, b=blue, c=(none); add two more reds
                Insert(c, "d", b => b.Set("color", "red"));
                Insert(c, "e", b => b.Set("color", "red"));

                using var q = c.GetSelectEntitiesQueryBase<Owner>();
                q.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "dp_color");
                // count owner ids per group (COUNT of the dynamic value would be COUNT(DISTINCT))
                q.AddToResultset(AggFn.Count, typeof(Owner), nameof(Owner.Id), "dp_count");
                q.AddDynamicPropertyToGroupBy<Owner>("color", DynamicPropertyValueType.String);

                var counts = new Dictionary<string, int>();
                foreach (var row in q.ReadAllDynamic())
                {
                    var dict = (IDictionary<string, object>)row;
                    counts[(string)(dict["dp_color"] ?? "(null)")] = Convert.ToInt32(dict["dp_count"]);
                }

                counts["red"].Should().Be(3);   // a, d, e
                counts["blue"].Should().Be(1);  // b
                counts["(null)"].Should().Be(1); // c has no color
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Having_FiltersGroupsByAggregate(string connectionName)
            => Run(connectionName, c =>
            {
                // reds: a(10) + d(5) + e(20) = 35; blue: b(30); c has no size
                Insert(c, "d", b => { b.Set("color", "red"); b.Set("size", 5); });
                Insert(c, "e", b => { b.Set("color", "red"); b.Set("size", 20); });

                using var q = c.GetSelectEntitiesQueryBase<Owner>();
                q.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "dp_color");
                q.AddDynamicPropertyToResultset<Owner>(AggFn.Sum, "size", DynamicPropertyValueType.Integer, "dp_sum");
                q.AddDynamicPropertyToGroupBy<Owner>("color", DynamicPropertyValueType.String);
                q.HavingDynamicPropertyOf<Owner>("size", DynamicPropertyValueType.Integer).Sum().Gt(30);

                var groups = new Dictionary<string, object>();
                foreach (var row in q.ReadAllDynamic())
                {
                    var dict = (IDictionary<string, object>)row;
                    groups[(string)(dict["dp_color"] ?? "(null)")] = dict["dp_sum"];
                }

                // only the red group has SUM(size) > 30; blue (30) and the null group drop out
                groups.Should().HaveCount(1);
                groups.Should().ContainKey("red");
                Convert.ToInt32(groups["red"]).Should().Be(35);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregate_Max_Integer(string connectionName)
            => Run(connectionName, c =>
            {
                using var q = c.GetSelectEntitiesQueryBase<Owner>();
                q.AddDynamicPropertyToResultset<Owner>(AggFn.Max, "size", DynamicPropertyValueType.Integer, "value");
                var row = (IDictionary<string, object>)q.ReadOneDynamic();
                row["value"].Should().Be(30);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregate_Count(string connectionName)
            => Run(connectionName, c =>
            {
                using var q = c.GetSelectEntitiesQueryBase<Owner>();
                q.AddDynamicPropertyToResultset<Owner>(AggFn.Count, "size", DynamicPropertyValueType.Integer, "value");
                var row = (IDictionary<string, object>)q.ReadOneDynamic();
                // Count renders COUNT(DISTINCT v_int); sizes 10 and 30 are two distinct non-null values.
                Convert.ToInt32(row["value"]).Should().Be(2);
            });
    }
}

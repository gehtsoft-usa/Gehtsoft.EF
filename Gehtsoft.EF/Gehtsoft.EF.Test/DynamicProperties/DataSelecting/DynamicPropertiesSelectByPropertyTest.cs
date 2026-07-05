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
    /// Filtering SelectEntitiesQuery and SelectEntitiesCountQuery by a dynamic-property condition, on
    /// every driver. Reading never touches the side table, so the condition is just
    /// `owner.id IN (SELECT owner FROM &lt;t&gt;_props WHERE ...)` - no cascade, no materialization.
    /// </summary>
    public class DynamicPropertiesSelectByPropertyTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;
        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);
        public DynamicPropertiesSelectByPropertyTest(SqlConnectionFixtureBase fixture) { mFixture = fixture; }

        [Entity(Scope = "dp_sel", Table = "dp_sel_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static void Insert(SqlDbConnection c, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = c.GetInsertEntityQuery<Owner>()) q.Execute(e);
        }

        private static void Seed(SqlDbConnection c)
        {
            Insert(c, "red10", b => { b.Set("color", "red"); b.Set("size", 10); });
            Insert(c, "red20", b => { b.Set("color", "red"); b.Set("size", 20); });
            Insert(c, "blue30", b => { b.Set("color", "blue"); b.Set("size", 30); });
            Insert(c, "green30", b => { b.Set("color", "green"); b.Set("size", 30); });
        }

        private static List<string> SelectNames(SqlDbConnection c, Action<EntityQueryConditionBuilder> where)
        {
            using (var q = c.GetSelectEntitiesQuery<Owner>())
            {
                where(q.Where);
                var names = new List<string>();
                foreach (var o in q.ReadAll<Owner>())
                    names.Add(o.Name);
                return names;
            }
        }

        private static int Count(SqlDbConnection c, Action<EntityQueryConditionBuilder> where)
        {
            using (var q = c.GetSelectEntitiesCountQuery<Owner>())
            {
                where(q.Where);
                return q.RowCount;
            }
        }

        private void Run(string connectionName, Action<SqlDbConnection> body)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var q = c.GetDropEntityQuery<Owner>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Owner>()) q.Execute();
            try { Seed(c); body(c); }
            finally { using (var q = c.GetDropEntityQuery<Owner>()) q.Execute(); }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_ByDynamicProperty_Eq(string connectionName)
            => Run(connectionName, c =>
                SelectNames(c, w => w.DynamicPropertyOf<Owner>("color").Eq("red"))
                    .Should().BeEquivalentTo(new[] { "red10", "red20" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Count_ByDynamicProperty_Eq(string connectionName)
            => Run(connectionName, c =>
                Count(c, w => w.DynamicPropertyOf<Owner>("color").Eq("red")).Should().Be(2));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_ByDynamicProperty_And(string connectionName)
            => Run(connectionName, c =>
                SelectNames(c, w => w.DynamicPropertyOf<Owner>("color").Eq("red")
                                     .And().DynamicPropertyOf<Owner>("size").Eq(20))
                    .Should().BeEquivalentTo(new[] { "red20" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_ByDynamicProperty_Or(string connectionName)
            => Run(connectionName, c =>
                SelectNames(c, w => w.DynamicPropertyOf<Owner>("color").Eq("blue")
                                     .Or().DynamicPropertyOf<Owner>("color").Eq("green"))
                    .Should().BeEquivalentTo(new[] { "blue30", "green30" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Count_ByDynamicProperty_Range(string connectionName)
            => Run(connectionName, c =>
                Count(c, w => w.DynamicPropertyOf<Owner>("size").Ge(30)).Should().Be(2)); // blue30 + green30

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_ComposedWithRegularColumn(string connectionName)
            => Run(connectionName, c =>
                SelectNames(c, w => w.Property(nameof(Owner.Name)).Like("red%")
                                     .And().DynamicPropertyOf<Owner>("size").Eq(10))
                    .Should().BeEquivalentTo(new[] { "red10" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_ByDynamicProperty_NoMatch_Empty(string connectionName)
            => Run(connectionName, c =>
                SelectNames(c, w => w.DynamicPropertyOf<Owner>("color").Eq("nosuch"))
                    .Should().BeEmpty());
    }
}

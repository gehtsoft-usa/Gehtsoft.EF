using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertiesFilterTest
    {
        [Entity(Scope = "dp_filter", Table = "dp_filter_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static int Insert(SqlDbConnection connection, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);
            return e.Id;
        }

        private static List<int> Ids(SqlDbConnection connection, Action<EntityQueryConditionBuilder> where)
        {
            using (var query = connection.GetSelectEntitiesQuery<Owner>())
            {
                where(query.Where);
                var list = query.ReadAll<Owner>();
                var ids = new List<int>();
                foreach (var o in list)
                    ids.Add(o.Id);
                return ids;
            }
        }

        private sealed class Seeded : IDisposable
        {
            public SqlDbConnection Connection { get; }
            public int Red10 { get; }
            public int Red20 { get; }
            public int Blue10 { get; }
            public int Green { get; }
            public int NoProps { get; }

            public Seeded()
            {
                Connection = SqliteDbConnectionFactory.CreateMemory();
                using (var q = Connection.GetCreateEntityQuery<Owner>())
                    q.Execute();

                Red10 = Insert(Connection, "red10", b => { b.Set("color", "red"); b.Set("size", 10); });
                Red20 = Insert(Connection, "red20", b => { b.Set("color", "red"); b.Set("size", 20); });
                Blue10 = Insert(Connection, "blue10", b => { b.Set("color", "blue"); b.Set("size", 10); });
                Green = Insert(Connection, "green", b => { b.Set("color", "green"); b.Set("size", 30); });
                NoProps = Insert(Connection, "noprops", null);
            }

            public void Dispose() => Connection.Dispose();
        }

        [Fact]
        public void Eq_SingleProperty()
        {
            using var s = new Seeded();
            var ids = Ids(s.Connection, w => w.DynamicPropertyOf<Owner>("color").Eq("red"));
            ids.Should().BeEquivalentTo(new[] { s.Red10, s.Red20 });
        }

        [Fact]
        public void And_TwoProperties()
        {
            using var s = new Seeded();
            var ids = Ids(s.Connection, w =>
                w.DynamicPropertyOf<Owner>("color").Eq("red").And().DynamicPropertyOf<Owner>("size").Eq(20));
            ids.Should().BeEquivalentTo(new[] { s.Red20 });
        }

        [Fact]
        public void Or_TwoProperties()
        {
            using var s = new Seeded();
            var ids = Ids(s.Connection, w =>
                w.DynamicPropertyOf<Owner>("color").Eq("blue").Or().DynamicPropertyOf<Owner>("color").Eq("green"));
            ids.Should().BeEquivalentTo(new[] { s.Blue10, s.Green });
        }

        [Fact]
        public void Ge_IntegerRange()
        {
            using var s = new Seeded();
            var ids = Ids(s.Connection, w => w.DynamicPropertyOf<Owner>("size").Ge(20));
            ids.Should().BeEquivalentTo(new[] { s.Red20, s.Green });
        }

        [Fact]
        public void Neq_RequiresPropertySet()
        {
            using var s = new Seeded();
            // "has a color, and it is not red" - excludes the red ones AND the one with no color
            var ids = Ids(s.Connection, w => w.DynamicPropertyOf<Owner>("color").Neq("red"));
            ids.Should().BeEquivalentTo(new[] { s.Blue10, s.Green });
        }

        [Fact]
        public void AndNot_IncludesUnset()
        {
            using var s = new Seeded();
            // "does NOT have color = red" - includes the owner that has no color at all
            var ids = Ids(s.Connection, w => w.AndNot().DynamicPropertyOf<Owner>("color").Eq("red"));
            ids.Should().BeEquivalentTo(new[] { s.Blue10, s.Green, s.NoProps });
        }

        [Fact]
        public void ComposesWithRegularProperty()
        {
            using var s = new Seeded();
            var ids = Ids(s.Connection, w =>
                w.Property(nameof(Owner.Name)).Like("red%").And().DynamicPropertyOf<Owner>("size").Eq(20));
            ids.Should().BeEquivalentTo(new[] { s.Red20 });
        }

        [Fact]
        public void SupportsAllValueTypes()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            var dt = new DateTime(2020, 5, 1, 12, 0, 0, DateTimeKind.Utc);
            int target = Insert(connection, "t", b =>
            {
                b.Set("flag", true);
                b.Set("big", 5_000_000_000L);
                b.Set("ratio", 1.5);
                b.Set("when", dt);
            });
            Insert(connection, "other", b =>
            {
                b.Set("flag", false);
                b.Set("big", 1L);
                b.Set("ratio", 2.5);
                b.Set("when", dt.AddDays(1));
            });

            Ids(connection, w => w.DynamicPropertyOf<Owner>("flag").Eq(true)).Should().BeEquivalentTo(new[] { target });
            Ids(connection, w => w.DynamicPropertyOf<Owner>("big").Eq(5_000_000_000L)).Should().BeEquivalentTo(new[] { target });
            Ids(connection, w => w.DynamicPropertyOf<Owner>("ratio").Eq(1.5)).Should().BeEquivalentTo(new[] { target });
            Ids(connection, w => w.DynamicPropertyOf<Owner>("when").Eq(dt)).Should().BeEquivalentTo(new[] { target });
        }
    }
}

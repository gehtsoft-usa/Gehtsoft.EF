using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    /// <summary>
    /// MultiDelete cascade for a dynamic-property owner, exercised on every configured driver.
    /// Regular-column and delete-all conditions cascade the property rows; a condition that filters
    /// on a dynamic property value is (for now) rejected with NotSupportedException - see
    /// CLAUDE/ENTITY_WHERE_PROBLEM.md.
    /// </summary>
    public class DynamicPropertiesMultiDeleteTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public DynamicPropertiesMultiDeleteTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        [Entity(Scope = "dp_mdel", Table = "dp_mdel_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static int AllProps(SqlDbConnection c)
        {
            using (var q = c.GetQuery("SELECT COUNT(*) FROM dp_mdel_owner_props", true))
            {
                q.ExecuteReader();
                q.ReadNext();
                return q.GetValue<int>(0);
            }
        }

        private static int OwnerCount(SqlDbConnection c)
        {
            using (var q = c.GetSelectEntitiesQuery<Owner>())
                return q.ReadAll<Owner>().Count;
        }

        // The names of the owners still present - so tests can assert *which* rows survived, not just
        // how many (deleting the wrong set could keep the same counts).
        private static System.Collections.Generic.List<string> Names(SqlDbConnection c)
        {
            using (var q = c.GetSelectEntitiesQuery<Owner>())
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var o in q.ReadAll<Owner>())
                    names.Add(o.Name);
                return names;
            }
        }

        private static void Insert(SqlDbConnection connection, string name, Action<DynamicPropertyBag> fill)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            fill?.Invoke(bag);
            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);
        }

        private static void Seed(SqlDbConnection connection)
        {
            Insert(connection, "red1", b => { b.Set("color", "red"); b.Set("size", 10); });
            Insert(connection, "red2", b => { b.Set("color", "red"); b.Set("size", 20); });
            Insert(connection, "blue1", b => { b.Set("color", "blue"); b.Set("size", 30); });
            Insert(connection, "green1", b => { b.Set("color", "green"); });
            // 2 + 2 + 2 + 1 = 7 property rows
        }

        private static void Recreate(SqlDbConnection connection)
        {
            using (var q = connection.GetDropEntityQuery<Owner>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();
        }

        private static void Drop(SqlDbConnection connection)
        {
            using (var q = connection.GetDropEntityQuery<Owner>())
                q.Execute();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void MultiDelete_ByRegularProperty_CascadesProps(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            Recreate(connection);
            try
            {
                Seed(connection);
                AllProps(connection).Should().Be(7);

                using (var query = connection.GetMultiDeleteEntityQuery<Owner>())
                {
                    query.Where.Property(nameof(Owner.Name)).Eq("red1");
                    query.Execute();
                }

                Names(connection).Should().BeEquivalentTo(new[] { "red2", "blue1", "green1" }); // red1 gone
                AllProps(connection).Should().Be(5); // red1's 2 rows cascaded away
            }
            finally
            {
                Drop(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void MultiDelete_NoCondition_DeletesEverything(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            Recreate(connection);
            try
            {
                Seed(connection);

                using (var query = connection.GetMultiDeleteEntityQuery<Owner>())
                    query.Execute();

                OwnerCount(connection).Should().Be(0);
                AllProps(connection).Should().Be(0);
            }
            finally
            {
                Drop(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void MultiDelete_ByDynamicProperty_DeletesMatchedAndCascades(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            Recreate(connection);
            try
            {
                Seed(connection);
                AllProps(connection).Should().Be(7);

                using (var query = connection.GetMultiDeleteEntityQuery<Owner>())
                {
                    query.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    query.Execute();
                }

                // the two RED owners are gone with their property rows; blue + green remain intact
                Names(connection).Should().BeEquivalentTo(new[] { "blue1", "green1" });
                AllProps(connection).Should().Be(3); // blue1(2) + green1(1)
            }
            finally
            {
                Drop(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void MultiDelete_ByComposedDynamicProperty_OnlyMatched(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            Recreate(connection);
            try
            {
                Seed(connection);

                // color = red AND size = 20  -> only red2
                using (var query = connection.GetMultiDeleteEntityQuery<Owner>())
                {
                    query.Where.DynamicPropertyOf<Owner>("color").Eq("red")
                               .And().DynamicPropertyOf<Owner>("size").Eq(20);
                    query.Execute();
                }

                Names(connection).Should().BeEquivalentTo(new[] { "red1", "blue1", "green1" }); // only red2 gone
                AllProps(connection).Should().Be(5); // 7 - red2's 2
            }
            finally
            {
                Drop(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async System.Threading.Tasks.Task MultiDelete_ByDynamicProperty_Async(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            Recreate(connection);
            try
            {
                Seed(connection);

                using (var query = connection.GetMultiDeleteEntityQuery<Owner>())
                {
                    query.Where.DynamicPropertyOf<Owner>("color").Eq("red");
                    await query.ExecuteAsync();
                }

                Names(connection).Should().BeEquivalentTo(new[] { "blue1", "green1" }); // red owners gone
                AllProps(connection).Should().Be(3);
            }
            finally
            {
                Drop(connection);
            }
        }
    }
}

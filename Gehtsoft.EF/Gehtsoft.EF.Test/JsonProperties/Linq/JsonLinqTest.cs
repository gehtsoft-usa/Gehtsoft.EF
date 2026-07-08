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

namespace Gehtsoft.EF.Test.JsonProperties.Linq
{
    // JSON values used directly in the entity LINQ surface (GetCollectionOf<T>): a member chain
    // (e.Profile.Address.State), an array element (e.Profile.Scores[0]), in WHERE / Select / OrderBy /
    // GroupBy / aggregate. bool and DateTime go through the per-driver value codec.
    public class JsonLinqTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;
        public static TheoryData<string> ConnectionNames(string flags = "") => SqlConnectionSources.SqlConnectionNames(flags);
        public JsonLinqTest(SqlConnectionFixtureBase fixture) { mFixture = fixture; }

        public class Address
        {
            public string City { get; set; }
            public string State { get; set; }
        }

        public class Profile
        {
            public int Age { get; set; }
            public decimal Income { get; set; }
            public bool Active { get; set; }
            public DateTime Since { get; set; }
            public int[] Scores { get; set; }
            public Address Address { get; set; }
        }

        [Entity(Scope = "json_linq", Table = "json_linq_p")]
        public class Person
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            [JsonEntityProperty(Nullable = true)] public Profile Profile { get; set; }
        }

        private static readonly DateTime AliceSince = new DateTime(2020, 1, 1);
        private static readonly DateTime BobSince = new DateTime(2022, 6, 15);
        private static readonly DateTime CarolSince = new DateTime(2019, 3, 10);
        private static readonly DateTime Cutoff = new DateTime(2021, 1, 1);

        private static void Insert(SqlDbConnection c, string name, Profile p)
        {
            using var q = c.GetInsertEntityQuery<Person>();
            q.Execute(new Person { Name = name, Profile = p });
        }

        private static void Seed(SqlDbConnection c)
        {
            Insert(c, "alice", new Profile { Age = 30, Income = 100000m, Active = true, Since = AliceSince, Scores = new[] { 15, 5 }, Address = new Address { City = "LA", State = "CA" } });
            Insert(c, "bob", new Profile { Age = 40, Income = 150000m, Active = false, Since = BobSince, Scores = new[] { 8 }, Address = new Address { City = "NYC", State = "NY" } });
            Insert(c, "carol", new Profile { Age = 25, Income = 90000m, Active = true, Since = CarolSince, Scores = new[] { 20, 11 }, Address = new Address { City = "SF", State = "CA" } });
        }

        private void Run(string connectionName, Action<SqlDbConnection> body)
        {
            var c = mFixture.GetInstance(connectionName);
            using (var q = c.GetDropEntityQuery<Person>()) q.Execute();
            using (var q = c.GetCreateEntityQuery<Person>()) q.Execute();
            try { Seed(c); body(c); }
            finally { using var q = c.GetDropEntityQuery<Person>(); q.Execute(); }
        }

        private static HashSet<string> Names(IEnumerable<Person> people)
        {
            var set = new HashSet<string>();
            foreach (var p in people)
                set.Add(p.Name);
            return set;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Where_Numeric(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Person>().Where(e => e.Profile.Age >= 30).ToList())
                    .Should().BeEquivalentTo(new[] { "alice", "bob" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Where_SubObject_String(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Person>().Where(e => e.Profile.Address.State == "CA").ToList())
                    .Should().BeEquivalentTo(new[] { "alice", "carol" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Where_ArrayElement(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Person>().Where(e => e.Profile.Scores[0] >= 10).ToList())
                    .Should().BeEquivalentTo(new[] { "alice", "carol" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Where_Bool(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Person>().Where(e => e.Profile.Active == true).ToList())
                    .Should().BeEquivalentTo(new[] { "alice", "carol" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Where_DateTime(string connectionName)
            => Run(connectionName, c =>
            {
                var cutoff = Cutoff;   // the LINQ compiler needs a local/literal, not a static field
                Names(c.GetCollectionOf<Person>().Where(e => e.Profile.Since >= cutoff).ToList())
                    .Should().BeEquivalentTo(new[] { "bob" });
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Where_Composed(string connectionName)
            => Run(connectionName, c =>
                Names(c.GetCollectionOf<Person>().Where(e => e.Profile.Address.State == "CA" && e.Profile.Age >= 30).ToList())
                    .Should().BeEquivalentTo(new[] { "alice" }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Select_SingleValue(string connectionName)
            => Run(connectionName, c =>
                c.GetCollectionOf<Person>().Select(e => e.Profile.Age).ToList()
                    .Should().BeEquivalentTo(new[] { 30, 40, 25 }));

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Select_Anonymous_WithDecode(string connectionName)
            => Run(connectionName, c =>
            {
                var rows = c.GetCollectionOf<Person>()
                    .Select(e => new { e.Name, City = e.Profile.Address.City, First = e.Profile.Scores[0], Active = e.Profile.Active, Since = e.Profile.Since })
                    .ToList();

                var alice = rows.Single(r => r.Name == "alice");
                alice.City.Should().Be("LA");
                alice.First.Should().Be(15);
                alice.Active.Should().BeTrue();
                alice.Since.Should().Be(AliceSince);

                var bob = rows.Single(r => r.Name == "bob");
                bob.Active.Should().BeFalse();
                bob.Since.Should().Be(BobSince);
                bob.First.Should().Be(8);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void OrderBy_Numeric(string connectionName)
            => Run(connectionName, c =>
            {
                var names = new List<string>();
                foreach (var p in c.GetCollectionOf<Person>().OrderBy(e => e.Profile.Income).ToList())
                    names.Add(p.Name);
                names.Should().Equal("carol", "alice", "bob");
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void GroupBy_SubObject(string connectionName)
            => Run(connectionName, c =>
            {
                var groups = c.GetCollectionOf<Person>()
                    .GroupBy(e => e.Profile.Address.State)
                    .Select(g => new { State = g.Key, Count = g.Count() })
                    .ToList();

                groups.Should().HaveCount(2);
                groups.Single(g => g.State == "CA").Count.Should().Be(2);
                groups.Single(g => g.State == "NY").Count.Should().Be(1);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Aggregate_Max(string connectionName)
            => Run(connectionName, c =>
            {
                c.GetCollectionOf<Person>().Select(e => SqlFunction.Max(e.Profile.Age)).First().Should().Be(40);
                c.GetCollectionOf<Person>().Select(e => SqlFunction.Max(e.Profile.Since)).First().Should().Be(BobSince);
            });

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void WholeEntity_DeserializesDocument(string connectionName)
            => Run(connectionName, c =>
            {
                var people = c.GetCollectionOf<Person>().Where(e => e.Profile.Age >= 30).ToList();
                Names(people).Should().BeEquivalentTo(new[] { "alice", "bob" });
                var alice = people.Single(p => p.Name == "alice");
                alice.Profile.Should().NotBeNull();
                alice.Profile.Income.Should().Be(100000m);
                alice.Profile.Address.City.Should().Be("LA");
                alice.Profile.Scores.Should().Equal(15, 5);
            });
    }
}

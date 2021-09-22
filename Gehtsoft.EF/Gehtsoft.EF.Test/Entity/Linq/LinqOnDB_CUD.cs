using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Linq
{
    public class LinqOnDB_CUD : IClassFixture<LinqOnDB_CUD.Fixture>
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.SqlConnectionNames(flags, mFlags);

        [Entity(Scope = "linq4", Table = "LinqDict")]
        public class Dict
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        public class Fixture : SqlConnectionFixtureBase
        {
            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
                Create(connection);
            }

            private static void Drop(SqlDbConnection connection)
            {
                using (var query = connection.GetDropEntityQuery<Dict>())
                    query.Execute();
            }

            private static void Create(SqlDbConnection connection)
            {
                using (var query = connection.GetCreateEntityQuery<Dict>())
                    query.Execute();
            }
        }

        private readonly Fixture mFixture;

        public LinqOnDB_CUD(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Insert(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = connection.GetCollectionOf<Dict>();

            var d = new Dict() { Name = "d1" };
            dict.Insert(d);

            using var atEnd = new DelayedAction(() =>
            {
                using (var query = connection.GetDeleteEntityQuery<Dict>())
                    query.Execute(d);
            });

            d.ID.Should().BeGreaterThan(0);
            dict.Where(o => o.ID == d.ID).Count().Should().Be(1);
            var d1 = dict.Where(o => o.ID == d.ID).First();
            d1.Should().NotBeNull();
            d1.ID.Should().Be(d.ID);
            d1.Name.Should().Be(d.Name);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Save(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = connection.GetCollectionOf<Dict>();

            var d = new Dict() { Name = "d1" };
            dict.Insert(d);
            var id = d.ID;
            d.Name = "d2";
            dict.Update(d);
            d.ID.Should().Be(id);

            using var atEnd = new DelayedAction(() =>
            {
                using (var query = connection.GetDeleteEntityQuery<Dict>())
                    query.Execute(d);
            });

            var d1 = dict.Where(o => o.ID == d.ID).First();
            d1.Should().NotBeNull();
            d1.ID.Should().Be(d.ID);
            d1.Name.Should().Be(d.Name);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Delete(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dict = connection.GetCollectionOf<Dict>();

            var d = new Dict() { Name = "d1" };
            dict.Insert(d);

            using var atEnd = new DelayedAction(() =>
            {
                using (var query = connection.GetDeleteEntityQuery<Dict>())
                    query.Execute(d);
            });

            dict.Where(o => o.ID == d.ID).Count().Should().Be(1);
            dict.Delete(d);
            dict.Where(o => o.ID == d.ID).Count().Should().Be(0);
            dict.Where(o => o.ID == d.ID).FirstOrDefault().Should().BeNull();
        }
    }
}

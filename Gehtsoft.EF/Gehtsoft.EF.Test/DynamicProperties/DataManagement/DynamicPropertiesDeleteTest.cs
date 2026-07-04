using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    public class DynamicPropertiesDeleteTest
    {
        [Entity(Scope = "dp_delete", Table = "dp_delete_owner")]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dp_delete", Table = "dp_delete_plain")]
        public class PlainOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 16, Nullable = true)]
            public string Name { get; set; }
        }

        private static int CountProps(SqlDbConnection connection, int owner)
        {
            using (var query = connection.GetQuery($"SELECT COUNT(*) FROM dp_delete_owner_props WHERE owner = {owner}", true))
            {
                query.ExecuteReader();
                query.ReadNext();
                return query.GetValue<int>(0);
            }
        }

        private static bool OwnerExists(SqlDbConnection connection, int id)
        {
            using (var query = connection.GetQuery($"SELECT COUNT(*) FROM dp_delete_owner WHERE id = {id}", true))
            {
                query.ExecuteReader();
                query.ReadNext();
                return query.GetValue<int>(0) > 0;
            }
        }

        private static int InsertOwnerWithProps(SqlDbConnection connection, string name)
        {
            var e = new Owner { Name = name };
            var bag = e.InitializeDynamicProperties();
            bag.Set("s", "hello");
            bag.Set("i", 42);
            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);
            return e.Id;
        }

        [Fact]
        public void Delete_RemovesPropertyRowsAndOwner()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            int id = InsertOwnerWithProps(connection, "e1");
            CountProps(connection, id).Should().Be(2);

            var toDelete = new Owner { Id = id };
            using (var q = connection.GetDeleteEntityQuery<Owner>())
                q.Execute(toDelete);

            CountProps(connection, id).Should().Be(0);
            OwnerExists(connection, id).Should().BeFalse();
        }

        [Fact]
        public void Delete_OnlyRemovesTargetEntityProps()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            int id1 = InsertOwnerWithProps(connection, "e1");
            int id2 = InsertOwnerWithProps(connection, "e2");
            CountProps(connection, id1).Should().Be(2);
            CountProps(connection, id2).Should().Be(2);

            using (var q = connection.GetDeleteEntityQuery<Owner>())
                q.Execute(new Owner { Id = id1 });

            CountProps(connection, id1).Should().Be(0, "the deleted entity's props are removed");
            CountProps(connection, id2).Should().Be(2, "the other entity's props are untouched");
            OwnerExists(connection, id1).Should().BeFalse();
            OwnerExists(connection, id2).Should().BeTrue();
        }

        [Fact]
        public void Delete_WithoutLoadingBag_StillRemovesProps()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            int id = InsertOwnerWithProps(connection, "e1");

            // a fresh entity object carrying only the PK, no bag loaded
            var toDelete = new Owner { Id = id };
            toDelete.DynamicProperties.Should().BeNull();

            using (var q = connection.GetDeleteEntityQuery<Owner>())
                q.Execute(toDelete);

            CountProps(connection, id).Should().Be(0);
        }

        [Fact]
        public void Delete_OwnerWithNoProps_Succeeds()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            var e = new Owner { Name = "e1" };  // no dynamic properties
            using (var q = connection.GetInsertEntityQuery<Owner>())
                q.Execute(e);

            using (var q = connection.GetDeleteEntityQuery<Owner>())
                q.Execute(e);

            OwnerExists(connection, e.Id).Should().BeFalse();
            CountProps(connection, e.Id).Should().Be(0);
        }

        [Fact]
        public void Delete_PlainEntity_Unaffected()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<PlainOwner>())
                q.Execute();

            var e = new PlainOwner { Name = "p" };
            using (var q = connection.GetInsertEntityQuery<PlainOwner>())
                q.Execute(e);
            using (var q = connection.GetDeleteEntityQuery<PlainOwner>())
                q.Execute(e);
        }

        [Fact]
        public async System.Threading.Tasks.Task Delete_Async_RemovesProps()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetCreateEntityQuery<Owner>())
                q.Execute();

            int id = InsertOwnerWithProps(connection, "e1");

            var toDelete = new Owner { Id = id };
            using (var q = connection.GetDeleteEntityQuery<Owner>())
                await q.ExecuteAsync(toDelete);

            CountProps(connection, id).Should().Be(0);
            OwnerExists(connection, id).Should().BeFalse();
        }
    }
}

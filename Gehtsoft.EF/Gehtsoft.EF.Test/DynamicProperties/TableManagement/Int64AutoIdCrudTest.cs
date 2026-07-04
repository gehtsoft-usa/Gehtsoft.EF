using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.TableManagement
{
    /// <summary>
    /// The dynamic-property side table uses an Int64 autoincrement primary key, a combination the
    /// existing entity tests never exercised (they all use Int32 auto-ids). This test validates
    /// Int64 auto-id create/insert(read-back)/read/update/delete on every configured driver, using
    /// the direct entity-query API (no LINQ layer).
    /// </summary>
    public class Int64AutoIdCrudTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;

        [Entity(Scope = "dp_int64autoid", Table = "dp_int64autoid")]
        public class Int64Entity
        {
            // NOTE: [AutoId] hard-codes Int32; an Int64 auto-id is declared explicitly.
            [EntityProperty(PrimaryKey = true, Autoincrement = true)]
            public long Id { get; set; }

            [EntityProperty(Size = 64, Nullable = true)]
            public string Name { get; set; }
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public Int64AutoIdCrudTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Crud(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDropEntityQuery<Int64Entity>())
                query.Execute();
            using (var query = connection.GetCreateEntityQuery<Int64Entity>())
                query.Execute();

            try
            {
                // CREATE — the Int64 auto-id must be read back into the entity
                var a = new Int64Entity() { Name = "a" };
                var b = new Int64Entity() { Name = "b" };
                using (var query = connection.GetInsertEntityQuery<Int64Entity>())
                    query.Execute(a);
                using (var query = connection.GetInsertEntityQuery<Int64Entity>())
                    query.Execute(b);

                a.Id.Should().BeGreaterThan(0);
                b.Id.Should().BeGreaterThan(a.Id);

                // READ one by the Int64 id
                using (var query = connection.GetSelectEntitiesQuery<Int64Entity>())
                {
                    query.Where.Property(nameof(Int64Entity.Id)).Eq(a.Id);
                    var read = query.ReadOne<Int64Entity>();
                    read.Should().NotBeNull();
                    read.Id.Should().Be(a.Id);
                    read.Name.Should().Be("a");
                }

                using (var query = connection.GetSelectEntitiesQuery<Int64Entity>())
                    query.ReadAll<Int64Entity>().Count.Should().Be(2);

                // UPDATE
                a.Name = "a-updated";
                using (var query = connection.GetUpdateEntityQuery<Int64Entity>())
                    query.Execute(a);

                using (var query = connection.GetSelectEntitiesQuery<Int64Entity>())
                {
                    query.Where.Property(nameof(Int64Entity.Id)).Eq(a.Id);
                    query.ReadOne<Int64Entity>().Name.Should().Be("a-updated");
                }

                // DELETE
                using (var query = connection.GetDeleteEntityQuery<Int64Entity>())
                    query.Execute(a);

                using (var query = connection.GetSelectEntitiesQuery<Int64Entity>())
                {
                    var all = query.ReadAll<Int64Entity>();
                    all.Count.Should().Be(1);
                    all[0].Id.Should().Be(b.Id);
                }
            }
            finally
            {
                using (var query = connection.GetDropEntityQuery<Int64Entity>())
                    query.Execute();
            }
        }
    }
}

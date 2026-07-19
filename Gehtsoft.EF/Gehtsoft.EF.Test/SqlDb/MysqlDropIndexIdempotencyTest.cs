using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    /// <summary>
    /// The MySQL-family DROP INDEX builder must be idempotent (dropping an absent index is a no-op), like
    /// every other dialect's. MariaDB gets it from <c>DROP INDEX IF EXISTS</c>; MySQL 8 has no such clause,
    /// so the builder guards the drop with an information_schema check. This exercises both servers.
    /// </summary>
    public class MysqlDropIndexIdempotencyTest : IClassFixture<MysqlDropIndexIdempotencyTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public MysqlDropIndexIdempotencyTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> MysqlEngines() => SqlConnectionSources.SqlConnectionNames("+mysql");

        private static readonly TableDescriptor gTable = new TableDescriptor
        (
            "dropidx_idem",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true },
                new TableDescriptor.ColumnInfo { Name = "name", DbType = DbType.String, Size = 32, Nullable = true },
            }
        );

        private static void Exec(SqlDbConnection connection, AQueryBuilder builder)
        {
            using (var q = connection.GetQuery(builder))
                q.ExecuteNoData();
        }

        [Theory]
        [MemberData(nameof(MysqlEngines))]
        public void DropIndex_IsIdempotent(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            Exec(connection, connection.GetDropTableBuilder(gTable));
            Exec(connection, connection.GetCreateTableBuilder(gTable));
            try
            {
                // dropping an index that was never created must not error
                Exec(connection, connection.GetDropIndexBuilder(gTable, "byname"));
                connection.DoesObjectExist("dropidx_idem", "byname", "index").Should().BeFalse();

                // create it, confirm, drop it, confirm gone
                Exec(connection, connection.GetCreateIndexBuilder(gTable, new CompositeIndex("byname") { "name" }));
                connection.DoesObjectExist("dropidx_idem", "byname", "index").Should().BeTrue();
                Exec(connection, connection.GetDropIndexBuilder(gTable, "byname"));
                connection.DoesObjectExist("dropidx_idem", "byname", "index").Should().BeFalse();

                // dropping again (now absent) must still be a no-op
                Exec(connection, connection.GetDropIndexBuilder(gTable, "byname"));
                connection.DoesObjectExist("dropidx_idem", "byname", "index").Should().BeFalse();
            }
            finally
            {
                Exec(connection, connection.GetDropTableBuilder(gTable));
            }
        }
    }
}

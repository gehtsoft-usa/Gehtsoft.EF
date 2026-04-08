using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class NestedTransactionTests : IClassFixture<NestedTransactionTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public NestedTransactionTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        private static readonly TableDescriptor gTable = new TableDescriptor
        (
            "transactiontest",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo { Name = "vint_pk", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                new TableDescriptor.ColumnInfo { Name = "vstring", DbType = DbType.String, Size = 32},
            }
        );

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql,-oracle")]
        public void NestedTransactions(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            connection.GetLanguageSpecifics().SupportsTransactions.Should().Be(SqlDbLanguageSpecifics.TransactionSupport.Nested);

            DropTableBuilder dbuilder = connection.GetDropTableBuilder(gTable);
            CreateTableBuilder cbuilder = connection.GetCreateTableBuilder(gTable);
            InsertQueryBuilder ibuilder = connection.GetInsertQueryBuilder(gTable);

            SqlDbQuery query;

            using (query = connection.GetQuery(dbuilder))
                query.ExecuteNoData();
            TableDescriptor[] schema = connection.Schema();
            schema.Should().NotBeNull();
            schema.Contains(gTable.Name).Should().BeFalse();
            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (SqlDbTransaction t1 = connection.BeginTransaction())
            {
                using (query = connection.GetQuery(ibuilder))
                {
                    query.BindParam("vstring", "s1");
                    query.ExecuteNoData();
                }

                using (SqlDbTransaction t2 = connection.BeginTransaction())
                {
                    using (query = connection.GetQuery(ibuilder))
                    {
                        query.BindParam("vstring", "s2");
                        query.ExecuteNoData();
                    }
                    t2.Commit();
                }

                using (SqlDbTransaction t3 = connection.BeginTransaction())
                {
                    using (query = connection.GetQuery(ibuilder))
                    {
                        query.BindParam("vstring", "s3");
                        query.ExecuteNoData();
                    }
                    t3.Rollback();
                }

                using (query = connection.GetQuery(ibuilder))
                {
                    query.BindParam("vstring", "s4");
                    query.ExecuteNoData();
                }
                t1.Commit();
            }

            SelectQueryBuilder sbuilder = connection.GetSelectQueryBuilder(gTable);
            sbuilder.AddToResultset(AggFn.Count);
            sbuilder.Where.Property(gTable["vstring"]).Is(CmpOp.Eq).Parameter("vstring");

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s1");
                query.ExecuteReader();
                query.ReadNext();
                query.GetValue<int>(0).Should().Be(1);
            }

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s2");
                query.ExecuteReader();
                query.ReadNext();
                query.GetValue<int>(0).Should().Be(1);
            }

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s3");
                query.ExecuteReader();
                query.ReadNext();
                query.GetValue<int>(0).Should().Be(0);
            }

            using (query = connection.GetQuery(sbuilder))
            {
                query.BindParam("vstring", "s4");
                query.ExecuteReader();
                query.ReadNext();
                query.GetValue<int>(0).Should().Be(1);
            }
        }
    }
}

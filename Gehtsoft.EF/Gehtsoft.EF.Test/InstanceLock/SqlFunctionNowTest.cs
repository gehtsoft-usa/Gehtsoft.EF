using System;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.InstanceLock
{
    /// <summary>
    /// Cross-driver test of the two clock functions added to <see cref="SqlDbLanguageSpecifics.GetSqlFunction"/>:
    /// <see cref="SqlFunctionId.Now"/> (current UTC timestamp in the dialect's DateTime representation)
    /// and <see cref="SqlFunctionId.LinuxSeconds"/> (integer unix epoch seconds). Each expression is
    /// selected on every configured database and checked against the client's own current UTC time.
    ///
    /// The self-bootstrapping <c>ef_catalog_lock</c> table is used as a guaranteed single-row source
    /// (an acquire/release seeds one row), so no dedicated fixture entity is needed. This also
    /// exercises the projection path (<see cref="SelectQueryBuilder.AddExpressionToResultset"/> with
    /// <c>suppressScalarProtection</c>), since these expressions carry quoted literals on some dialects.
    /// </summary>
    public sealed class SqlFunctionNowTest : IClassFixture<SqlConnectionFixtureBase>
    {
        // Generous: independent of time zone (both sides are UTC / epoch), so this only has to absorb
        // client/server clock skew, yet is tight enough to catch a local-vs-UTC (hours-off) mistake.
        private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(10);

        private readonly SqlConnectionFixtureBase mFixture;

        public SqlFunctionNowTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        private static TableDescriptor ProbeTable()
        {
            TableDescriptor descriptor = new TableDescriptor(SqlDbConnection.InstanceLockTableName);
            descriptor.Add(new TableDescriptor.ColumnInfo() { ID = "Name", Name = "name", DbType = DbType.String, Size = 128, PrimaryKey = true, Nullable = false });
            return descriptor;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Now_And_LinuxSeconds_AreCurrent(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            const string probe = "eflock_fn_probe";

            // Seed a row (acquire+release leaves the row in place with a NULL owner).
            using (connection.AcquireInstanceLock(probe, TimeSpan.FromSeconds(5))) { }

            SqlDbLanguageSpecifics specifics = connection.GetLanguageSpecifics();
            string nowExpr = specifics.GetSqlFunction(SqlFunctionId.Now, null);
            string secondsExpr = specifics.GetSqlFunction(SqlFunctionId.LinuxSeconds, null);
            nowExpr.Should().NotBeNullOrEmpty();
            secondsExpr.Should().NotBeNullOrEmpty();

            TableDescriptor descriptor = ProbeTable();
            SelectQueryBuilder select = connection.GetSelectQueryBuilder(descriptor);
            select.SuppressScalarProtection = true;
            select.AddExpressionToResultset(nowExpr, DbType.DateTime, false, "now_val");
            select.AddExpressionToResultset(secondsExpr, DbType.Int64, false, "sec_val");
            select.Where.Property(descriptor["Name"]).Eq().Parameter("name");

            DateTime dbNow;
            long dbSeconds;
            using (var query = connection.GetQuery(select))
            {
                query.BindParam("name", probe);
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                dbNow = query.GetValue<DateTime>(0);
                dbSeconds = query.GetValue<long>(1);
            }

            DateTime clientNow = DateTime.UtcNow;
            long clientSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // The DB DateTime comes back with an unspecified kind but holds a UTC wall-clock value,
            // so a plain tick comparison against UtcNow is correct.
            (dbNow - clientNow).Duration().Should().BeLessThan(Tolerance);
            Math.Abs(dbSeconds - clientSeconds).Should().BeLessThan((long)Tolerance.TotalSeconds);
        }
    }
}

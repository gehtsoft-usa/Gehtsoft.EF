using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqliteDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    /// <summary>
    /// Exercises the custom scalar SQL functions the SQLite driver registers on every connection
    /// (SetupFunctions): the date-part extractors (default numeric/OADate storage) and the SLEFT / TOSTRING
    /// / TOREAL helpers. The callbacks only run when SQL invokes them, so these tests drive them through
    /// real queries on an in-memory database.
    /// </summary>
    public class SqliteCustomFunctionsTest
    {
        private static SqlDbConnection Memory()
        {
            var connection = SqliteDbConnectionFactory.CreateMemory();
            using (var q = connection.GetQuery("create table dt(n real, s text)"))
                q.ExecuteNoData();
            return connection;
        }

        [Fact]
        public void DatePartFunctions_NumericStorage()
        {
            using var connection = Memory();
            double oa = new DateTime(2021, 6, 15, 13, 24, 35).ToOADate();
            using (var q = connection.GetQuery("insert into dt(n, s) values(@n, null)"))
            {
                q.BindParam("n", oa);
                q.ExecuteNoData();
            }
            using (var q = connection.GetQuery("insert into dt(n, s) values(null, null)"))
                q.ExecuteNoData();

            using (var q = connection.GetQuery("select YEAR(n), MONTH(n), DAY(n), HOUR(n), MINUTE(n), SECOND(n) from dt where n is not null"))
            {
                q.ExecuteReader();
                q.ReadNext().Should().BeTrue();
                q.GetValue<int>(0).Should().Be(2021);
                q.GetValue<int>(1).Should().Be(6);
                q.GetValue<int>(2).Should().Be(15);
                q.GetValue<int>(3).Should().Be(13);
                q.GetValue<int>(4).Should().Be(24);
                q.GetValue<int>(5).Should().Be(35);
            }

            // null operand -> the functions return null (the d == null branch)
            using (var q = connection.GetQuery("select YEAR(n), MONTH(n), DAY(n), HOUR(n), MINUTE(n), SECOND(n) from dt where n is null"))
            {
                q.ExecuteReader();
                q.ReadNext().Should().BeTrue();
                q.IsNull(0).Should().BeTrue();
                q.IsNull(5).Should().BeTrue();
            }
        }

        [Fact]
        public void SLeft_HandlesShorterEqualLongerAndNull()
        {
            using var connection = Memory();
            using (var q = connection.GetQuery("insert into dt(n, s) values(1, 'x')", true))
                q.ExecuteNoData();

            using (var q = connection.GetQuery("select SLEFT('hello', 3), SLEFT('ab', 5), SLEFT('abc', 3), SLEFT(null, 2) from dt", true))
            {
                q.ExecuteReader();
                q.ReadNext().Should().BeTrue();
                q.GetValue<string>(0).Should().Be("hel"); // l < length
                q.GetValue<string>(1).Should().Be("ab");  // l > length -> clamped, returns whole string
                q.GetValue<string>(2).Should().Be("abc"); // l == length
                q.IsNull(3).Should().BeTrue();             // null input
            }
        }

        [Fact]
        public void ToStringAndToReal_HandleReachableTypes()
        {
            using var connection = Memory();
            using (var q = connection.GetQuery("insert into dt(n, s) values(2.5, 'hello')", true))
                q.ExecuteNoData();

            using (var q = connection.GetQuery("select TOSTRING(n), TOSTRING(s), TOSTRING(null), TOSTRING(9) from dt", true))
            {
                q.ExecuteReader();
                q.ReadNext().Should().BeTrue();
                q.GetValue<string>(0).Should().Be("2.5"); // double
                q.GetValue<string>(1).Should().Be("hello"); // string
                q.IsNull(2).Should().BeTrue();             // null
                q.GetValue<string>(3).Should().Be("9");    // integer -> fallthrough ToString
            }

            using (var q = connection.GetQuery("select TOREAL(n), TOREAL(s), TOREAL('2.5'), TOREAL('bad'), TOREAL(null), TOREAL(9) from dt", true))
            {
                q.ExecuteReader();
                q.ReadNext().Should().BeTrue();
                q.GetValue<double>(0).Should().Be(2.5);  // double
                q.GetValue<double>(2).Should().Be(2.5);  // parseable string
                q.GetValue<double>(3).Should().Be(0);    // unparseable string -> 0
                q.GetValue<double>(4).Should().Be(0);    // null -> 0
                q.GetValue<double>(5).Should().Be(9);    // integer/long
            }
        }
    }
}

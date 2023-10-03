using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.MysqlDb;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.PostgresDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    public class SqlDbConnectionTest
    {
        class LockTestParams
        {
            public bool locked;
            public int waslocked;
            public int locks;
            public SqlDbConnection connection;
        }

        private static void SyncAction(LockTestParams parameters)
        {
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.ElapsedMilliseconds < 3000)
            {
                using (var locking = parameters.connection.Lock())
                {
                    if (parameters.locked)
                        Interlocked.Increment(ref parameters.waslocked);
                    Interlocked.Increment(ref parameters.locks);
                    parameters.locked = true;
                    Thread.Sleep(1);
                    parameters.locked = false;
                }
            }
        }

        private static Task AsyncSyncAction(LockTestParams parameters)
            => Task.Run(() => SyncAction(parameters));

        private static async Task AsyncAction(LockTestParams parameters)
        {
            var stopWatch = Stopwatch.StartNew();

            while (stopWatch.ElapsedMilliseconds < 3000)
            {
                using (var locking = await parameters.connection.LockAsync())
                {
                    if (parameters.locked)
                        Interlocked.Increment(ref parameters.waslocked);
                    Interlocked.Increment(ref parameters.locks);
                    parameters.locked = true;
                    Thread.Sleep(1);
                    parameters.locked = false;
                }
            }
        }


        [Fact]
        public async Task LockTest()
        {
            var parameters = new LockTestParams()
            {
                locked = false,
                waslocked = 0,
                locks = 0,
                connection = new DummySqlConnection()
            };


            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
                tasks.Add(AsyncSyncAction(parameters));

            for (int i = 0; i < 10; i++)
                tasks.Add(AsyncAction(parameters));

            await Task.WhenAll(tasks.ToArray());

            parameters.locks.Should().BeGreaterThan(20);
            parameters.waslocked.Should().Be(0);
        }

        [Theory]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(object), null, "NULL")]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(short), 123, "123")]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(int), 123, "123")]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(long), 123, "123")]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(double), 1.23, "1.23")]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(decimal), 1.23, "1.23")]
        [InlineData(typeof(Sql92LanguageSpecifics), typeof(string), "abc", "'abc'")]

        [InlineData(typeof(MssqlDbLanguageSpecifics), typeof(bool), true, "1")]
        [InlineData(typeof(MssqlDbLanguageSpecifics), typeof(bool), false, "0")]
        [InlineData(typeof(MssqlDbLanguageSpecifics), typeof(DateTime), "2010-02-05", "{d '2010-02-05'}")]

        [InlineData(typeof(MysqlDbLanguageSpecifics), typeof(bool), true, "1")]
        [InlineData(typeof(MysqlDbLanguageSpecifics), typeof(bool), false, "0")]
        [InlineData(typeof(MysqlDbLanguageSpecifics), typeof(DateTime), "2010-02-05", "'2010-02-05'")]

        [InlineData(typeof(OracleDbLanguageSpecifics), typeof(bool), true, "1")]
        [InlineData(typeof(OracleDbLanguageSpecifics), typeof(bool), false, "0")]
        [InlineData(typeof(OracleDbLanguageSpecifics), typeof(string), "abc", "''abc''")]
        [InlineData(typeof(OracleDbLanguageSpecifics), typeof(DateTime), "2010-02-05", "DATE '2010-02-05'")]

        [InlineData(typeof(PostgresDbLanguageSpecifics), typeof(bool), true, "TRUE")]
        [InlineData(typeof(PostgresDbLanguageSpecifics), typeof(bool), false, "FALSE")]
        [InlineData(typeof(PostgresDbLanguageSpecifics), typeof(DateTime), "2010-02-05", "CAST('2010-02-05' AS DATE)")]

        [InlineData(typeof(SqliteDbLanguageSpecifics), typeof(bool), true, "1")]
        [InlineData(typeof(SqliteDbLanguageSpecifics), typeof(bool), false, "0")]
        [InlineData(typeof(SqliteDbLanguageSpecifics), typeof(DateTime), "2010-02-05", "40214")]

        public void LanguageSpecific_FormatValue(Type languageSpecificType, Type valueType, object value, string formattedValue)
        {
            value = TestValue.Translate(valueType, value);
            var specifics = Activator.CreateInstance(languageSpecificType) as SqlDbLanguageSpecifics;
            specifics.Should().NotBeNull();

            specifics.FormatValue(value).Should().Be(formattedValue);
        }
    }
}


using System;
using System.Collections.Generic;
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
        [Fact]
        public void LockTest()
        {
            bool locked = false;
            using var connection = new DummySqlConnection();
            int waslocked = 0;
            int locks = 0;

            Action syncAction = () =>
            {
                DateTime startTime = DateTime.Now;

                while (DateTime.Now - startTime < TimeSpan.FromSeconds(3))
                {
                    using (var locking = connection.Lock())
                    {
                        if (locked)
                            waslocked++;
                        locks++;
                        locked = true;
                        Thread.Sleep(1);
                        locked = false;
                    }
                }
            };

            Action asyncAction = () =>
            {
                DateTime startTime = DateTime.Now;

                while (DateTime.Now - startTime < TimeSpan.FromSeconds(3))
                {
                    using (var locking = connection.LockAsync().ConfigureAwait(false).GetAwaiter().GetResult())
                    {
                        if (locked)
                            waslocked++;
                        locks++;
                        locked = true;
                        Thread.Sleep(1);
                        locked = false;
                    }
                }
            };

            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
                tasks.Add(Task.Run(syncAction));

            for (int i = 0; i < 10; i++)
                tasks.Add(Task.Run(asyncAction));

            Task.WaitAll(tasks.ToArray());

            locks.Should().BeGreaterThan(20);
            waslocked.Should().Be(0);
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


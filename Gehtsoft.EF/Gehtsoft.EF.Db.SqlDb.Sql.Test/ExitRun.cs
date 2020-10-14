using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text;
using Xunit;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class ExitRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public ExitRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=:memory:"); ;
            Snapshot snapshot = new Snapshot();
            connection = connectionFactory.GetConnection();
            snapshot.CreateAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        public void Dispose()
        {
            if (connectionFactory.NeedDispose)
                connection.Dispose();
        }

        [Fact]
        public void Exit()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);
            List<object> array;

            func = environment.Parse("test",
                "DECLARE qqq AS STRING;" +
                "SET qqq = 'u';" +
                "SELECT COUNT(*) AS Total " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE ?qqq || '%' "
            );
            result = func(null);
            array = result as List<object>;
            int cnt1 = (int)(array[0] as Dictionary<string, object>)["Total"];

            func = environment.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SELECT * " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE 'u%' " +
                "SET qqq = ROWS_COUNT(LAST_RESULT());" +
                "EXIT WITH ?qqq"
            );
            result = func(null);
            int cnt2 = (int)result;

            cnt1.Should().Be(cnt2);
        }

        [Fact]
        public void ExitParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SET qqq = ROWS_COUNT(LAST_RESULT());" +
                "EXIT WITH Low"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SET qqq = ROWS_COUNT(UPPER('sss'));" +
                "EXIT WITH ?qqq"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "DECLARE qqq AS ROW;" +
                "SET qqq = GET_ROW(LAST_RESULT(), 0);" +
                "EXIT WITH GET_FIELD(?qqq, 'Number', GURMUR)"
                )
            );
        }
    }
}

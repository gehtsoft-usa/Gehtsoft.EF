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
        public void ExitWithRun()
        {
            object result;
            List<object> array;

            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();
            environment.Parse("test",
                "DECLARE qqq AS STRING;" +
                "SET qqq = 'u';" +
                "SELECT COUNT(*) AS Total " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE ?qqq || '%' "
            );
            result = environment.Run(connection);
            array = result as List<object>;
            int cnt1 = (int)(array[0] as Dictionary<string, object>)["Total"];

            environment.Parse("test",
                "DECLARE qqq AS INTEGER;" +
                "SELECT * " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE 'u%' " +
                "SET qqq = ROWS_COUNT(LAST_RESULT());" +
                "EXIT WITH ?qqq"
            );
            result = environment.Run(connection);
            int cnt2 = (int)result;

            cnt1.Should().Be(cnt2);
        }

        [Fact]
        public void ExitWithLinq()
        {
            Expression block;
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment(connection);
            List<object> array;

            block = environment.ParseToLinq("test",
                "DECLARE qqq AS STRING;" +
                "SET qqq = 'u';" +
                "SELECT COUNT(*) AS Total " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE ?qqq || '%' "
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            int cnt1 = (int)(array[0] as Dictionary<string, object>)["Total"];

            block = environment.ParseToLinq("test",
                "DECLARE qqq AS INTEGER;" +
                "SELECT * " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE 'u%' " +
                "SET qqq = ROWS_COUNT(LAST_RESULT());" +
                "EXIT WITH ?qqq"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            int cnt2 = (int)result;

            cnt1.Should().Be(cnt2);
        }
    }
}

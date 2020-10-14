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
    public class SwitchRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public SwitchRun()
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
        public void SwitchSuccess()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test",
                "DECLARE q AS INTEGER, m AS INTEGER;" +
                "SET q=4, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH " +
                "EXIT WITH ?m"
            );

            result = func(null);
            ((int)result).Should().Be(4);

            func = environment.Parse("test",
                "SET q=3, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "      SET m = 4" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(3);

            func = environment.Parse("test",
                "SET q=2, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(2);

            func = environment.Parse("test",
                "SET q=2, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(3);

            func = environment.Parse("test",
                "SET q=1, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(1);

            func = environment.Parse("test",
                "SET q=0, m=0" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SwitchParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET q=3, m=0;" +
                "SWITCH ?q " +
                "   CASE '2' :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET q='3', m=0;" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET m=0;" +
                "SWITCH ?q " +
                "   CASE 2 :" +
                "      SET m = 2" +
                "      BREAK" +
                "   CASE 3 :" +
                "      SET m = 3" +
                "      BREAK" +
                "   CASE 4 :" +
                "      SET m = 4" +
                "      BREAK" +
                "   OTHERWISE:" +
                "      SET m = 1" +
                "END SWITCH"
                )
            );
        }
    }
}

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
    public class IfRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public IfRun()
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
        public void IfSuccess1()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test",
                "SET q=4, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(4);

            func = environment.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(3);

            func = environment.Parse("test",
                "SET q=2, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(2);

            func = environment.Parse("test",
                "SET q=1, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(1);

            func = environment.Parse("test",
                "SET q=0, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void IfSuccess2()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test",
                "SET q=2, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(2);

            func = environment.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(3);

            func = environment.Parse("test",
                "SET q=4, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "END IF " +
                "EXIT WITH ?m"
            );
            result = func(null);
            ((int)result).Should().Be(0);
        }

        [Fact]
        public void IfParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSE" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF ?q = 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET q=3, m=0" +
                "IF ?q = 2 THEN" +
                "   SET m = 2;" +
                "ELSIF ?q = 3 THEN" +
                "   SET m = 3" +
                "ELSIF 4 THEN" +
                "   SET m = 4" +
                "ELSE" +
                "   SET m = 1" +
                "END IF"
                )
            );
        }
    }
}

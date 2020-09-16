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

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class ForDoRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public ForDoRun()
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
        public void ForDoSuccess()
        {
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();

            environment.Parse("test",
                "SET factorial = 1 " +
                "FOR SET n=0 WHILE ?n <= 5 NEXT SET n=?n+1 LOOP " +
                "   IF ?n = 0 THEN CONTINUE; END IF " +
                "   SET factorial = ?factorial * ?n " +
                "END LOOP " +
                "EXIT WITH ?factorial"
            );
            result = environment.Run(connection);
            ((int)result).Should().Be(120);
        }
    }
}

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
    public class DeclareCursorRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public DeclareCursorRun()
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
        public void DeclareCursorWithRun1()
        {
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();

            environment.Parse("test",
                "SET maxQuantity = 0.0;" +
                "DECLARE my_cur CURSOR FOR " +
                "SELECT Quantity FROM OrderDetail;" +
                "OPEN CURSOR ?my_cur " +
                "SET record = FETCH(?my_cur) " +
                "WHILE ?record IS NOT NULL " +
                "LOOP " +
                "   SET quantity = GET_FIELD(?record, 'Quantity', DOUBLE);" +
                "   IF ?quantity > ?maxQuantity THEN SET maxQuantity = ?quantity; END IF;" +
                "   SET record = FETCH(?my_cur);" +
                "END LOOP;" +
                "CLOSE CURSOR ?my_cur;" +
                "EXIT WITH ?maxQuantity;"
            );
            result = environment.Run(connection);
            double max1 = (double)result;

            environment.Parse("test",
                "SELECT MAX(Quantity) AS Max FROM OrderDetail;" +
                "EXIT WITH GET_FIELD(GET_ROW(LAST_RESULT(), 0), 'Max', DOUBLE);"
            );
            result = environment.Run(connection);
            double max2 = (double)result;

            max1.Should().Be(max2);
        }

        [Fact]
        public void DeclareCursorWithRun2()
        {
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment();

            environment.Parse("test",
                "SET maxQuantity = 0.0;" +
                "DECLARE my_cur CURSOR FOR " +
                "SELECT COUNT(*) AS Total FROM Category;" +
                "OPEN CURSOR ?my_cur;" +
                "SET record = FETCH(?my_cur);" +
                "SET cnt = GET_FIELD(?record, 'Total', INTEGER);" +
                "CLOSE CURSOR ?my_cur;" +
                "EXIT WITH ?cnt;"
            );
            result = environment.Run(connection);
            int count = (int)result;
            count.Should().Be(8);
        }

        [Fact]
        public void DeclareCursorWithLinq1()
        {
            Expression block;
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment(connection);

            block = environment.ParseToLinq("test",
                "SET maxQuantity = 0.0;" +
                "DECLARE my_cur CURSOR FOR " +
                "SELECT Quantity FROM OrderDetail;" +
                "OPEN CURSOR ?my_cur " +
                "SET record = FETCH(?my_cur) " +
                "WHILE ?record IS NOT NULL " +
                "LOOP " +
                "   SET quantity = GET_FIELD(?record, 'Quantity', DOUBLE);" +
                "   IF ?quantity > ?maxQuantity THEN SET maxQuantity = ?quantity; END IF;" +
                "   SET record = FETCH(?my_cur);" +
                "END LOOP;" +
                "CLOSE CURSOR ?my_cur;" +
                "EXIT WITH ?maxQuantity;"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            double max1 = (double)result;

            block = environment.ParseToLinq("test",
                "SELECT MAX(Quantity) AS Max FROM OrderDetail;" +
                "EXIT WITH GET_FIELD(GET_ROW(LAST_RESULT(), 0), 'Max', DOUBLE);"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            double max2 = (double)result;

            max1.Should().Be(max2);
        }

        [Fact]
        public void DeclareCursorWithLinq2()
        {
            Expression block;
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment(connection);

            block = environment.ParseToLinq("test",
                "SET maxQuantity = 0.0;" +
                "DECLARE my_cur CURSOR FOR " +
                "SELECT COUNT(*) AS Total FROM Category;" +
                "OPEN CURSOR ?my_cur;" +
                "SET record = FETCH(?my_cur);" +
                "SET cnt = GET_FIELD(?record, 'Total', INTEGER);" +
                "CLOSE CURSOR ?my_cur;" +
                "EXIT WITH ?cnt;"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            int count = (int)result;
            count.Should().Be(8);
        }
    }
}

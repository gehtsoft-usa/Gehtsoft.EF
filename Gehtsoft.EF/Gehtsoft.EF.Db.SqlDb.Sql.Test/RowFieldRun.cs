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
    public class RowFieldRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public RowFieldRun()
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

        //[Fact]
        //public void Empty()
        //{
        //}

        [Fact]
        public void AddRowField()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            Dictionary<string, object> dict;
            List<object> array;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test",
                "SELECT Quantity FROM OrderDetail LIMIT 1;" +
                "SET record = GET_ROW(LAST_RESULT(), 0);" +
                "ADD FIELD 'Test' WITH 'testing' TO ?record;" +
                "EXIT WITH ?record"
            );
            result = func(null);
            dict = result as Dictionary<string, object>;
            dict.ContainsKey("Test").Should().BeTrue();
            dict.ContainsKey("Quantity").Should().BeTrue();
            string test = (string)dict["Test"];
            test.Should().Be("testing");

            func = environment.Parse("test",
                "SET recordset = NEW_ROWSET();" +
                "SET record1 = NEW_ROW(), record2 = NEW_ROW();" +
                "ADD FIELD 'Test' WITH 'testing' TO ?record1;" +
                "ADD FIELD 'Total' WITH 1 TO ?record1;" +
                "ADD ROW ?record1 TO ?recordset;" +
                "ADD FIELD 'Test' WITH 'testing' TO ?record2;" +
                "ADD FIELD 'Total' WITH 2 TO ?record2;" +
                "ADD ROW ?record2 TO ?recordset;" +
                "EXIT WITH ?recordset"
            );
            result = func(null);
            array = result as List<object>;
            array.Count.Should().Be(2);
            dict = array[0] as Dictionary<string, object>;
            dict.ContainsKey("Test").Should().BeTrue();
            dict.ContainsKey("Total").Should().BeTrue();
            string test1 = (string)dict["Test"];
            test1.Should().Be("testing");
            int total1 = (int)dict["Total"];
            total1.Should().Be(1);
            dict = array[1] as Dictionary<string, object>;
            dict.ContainsKey("Test").Should().BeTrue();
            dict.ContainsKey("Total").Should().BeTrue();
            string test2 = (string)dict["Test"];
            test2.Should().Be("testing");
            int total2 = (int)dict["Total"];
            total2.Should().Be(2);
        }

        [Fact]
        public void GetRowField()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test",
                "SET maxQuantity = 0.0;" +
                "SELECT Quantity FROM OrderDetail;" +
                "SET qqq = LAST_RESULT(), count = ROWS_COUNT(?qqq);" +
                "FOR SET n=0 WHILE ?n < ?count NEXT SET n=?n+1 LOOP " +
                "   SET record = GET_ROW(?qqq, ?n);" +
                "   SET quantity = GET_FIELD(?record, 'Quantity', DOUBLE);" +
                "   IF ?quantity > ?maxQuantity THEN SET maxQuantity = ?quantity; END IF;" +
                "END LOOP " +
                "EXIT WITH ?maxQuantity"
            );
            result = func(null);
            double max1 = (double)result;

            func = environment.Parse("test",
                "SELECT MAX(Quantity) AS Max FROM OrderDetail;" +
                "EXIT WITH GET_FIELD(GET_ROW(LAST_RESULT(), 0), 'Max', DOUBLE)"
            );
            result = func(null);
            double max2 = (double)result;

            max1.Should().Be(max2);
        }
    }
}

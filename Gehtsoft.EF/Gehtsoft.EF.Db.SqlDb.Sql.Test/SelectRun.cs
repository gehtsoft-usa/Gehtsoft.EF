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
    public class SelectRun
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;

        public SelectRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=d:\testsql.db"); ;
            Snapshot snapshot = new Snapshot();
            snapshot.CreateAsync(connectionFactory.GetConnection()).ConfigureAwait(true).GetAwaiter().GetResult();
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        [Fact]
        public void SimpleSelectAll()
        {
            DomBuilder.Parse("test", "SELECT * FROM Category");
            object result = DomBuilder.Run(connectionFactory);
            List<object> array = result as List<object>;
            array.Count().Should().Be(8);
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryID").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("Description").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectFields()
        {
            DomBuilder.Parse("test", "SELECT CategoryID AS Id, CategoryName FROM Category");
            object result = DomBuilder.Run(connectionFactory);
            List<object> array = result as List<object>;
            array.Count().Should().Be(8);
            (array[0] as Dictionary<string, object>).ContainsKey("Id").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectCount()
        {
            DomBuilder.Parse("test", "SELECT COUNT(*) AS Total FROM Category");
            object result = DomBuilder.Run(connectionFactory);
            List<object> array = result as List<object>;
            ((int)(array[0] as Dictionary<string, object>)["Total"]).Should().Be(8);
        }

        [Fact]
        public void SimpleSelectAgg()
        {
            DomBuilder.Parse("test", "SELECT MAX(OrderDate) AS Max, MIN(OrderDate) AS Min FROM Order");
            object result = DomBuilder.Run(connectionFactory);
            List<object> array = result as List<object>;
            DateTime max = (DateTime)(array[0] as Dictionary<string, object>)["Max"];
            DateTime min = (DateTime)(array[0] as Dictionary<string, object>)["Min"];
            (max > min).Should().BeTrue();
        }
    }
}

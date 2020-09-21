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
    public class DeleteRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public DeleteRun()
        {
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=d:\testsql.db"); ;
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
        public void DeleteSuccess()
        {
            object result;
            List<object> array;

            DomBuilder.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            int countBefore = (int)(array[0] as Dictionary<string, object>)["Total"];

            DomBuilder.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            Int64 insertedID = (Int64)(array[0] as Dictionary<string, object>)["LastInsertedId"];

            DomBuilder.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            int countAfterInsert = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterInsert.Should().Be(countBefore + 1);

            DomBuilder.Parse("test", $"DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            int deleted = (int)(array[0] as Dictionary<string, object>)["Deleted"];
            deleted.Should().Be(1);

            DomBuilder.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            int countAfterDelete = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterDelete.Should().Be(countBefore);

            DomBuilder.Parse("test", $"DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            deleted = (int)(array[0] as Dictionary<string, object>)["Deleted"];
            deleted.Should().Be(0);

            DomBuilder.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            int countAfterDelete1 = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterDelete1.Should().Be(countBefore);
        }

        [Fact]
        public void DeleteSuccessToLinq()
        {
            Expression block;
            object result;
            SqlCodeDomBuilder environment = DomBuilder.NewEnvironment(connection);
            List<object> array;

            block = environment.ParseToLinq("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            int countBefore = (int)(array[0] as Dictionary<string, object>)["Total"];

            block = environment.ParseToLinq("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            Int64 insertedID = (Int64)(array[0] as Dictionary<string, object>)["LastInsertedId"];

            block = environment.ParseToLinq("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            int countAfterInsert = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterInsert.Should().Be(countBefore + 1);

            block = environment.ParseToLinq("test", $"DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            int deleted = (int)(array[0] as Dictionary<string, object>)["Deleted"];
            deleted.Should().Be(1);

            block = environment.ParseToLinq("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            int countAfterDelete = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterDelete.Should().Be(countBefore);

            block = environment.ParseToLinq("test", $"DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            deleted = (int)(array[0] as Dictionary<string, object>)["Deleted"];
            deleted.Should().Be(0);

            block = environment.ParseToLinq("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = Expression.Lambda<Func<object>>(block).Compile()();
            array = result as List<object>;
            int countAfterDelete1 = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterDelete1.Should().Be(countBefore);
        }
    }
}

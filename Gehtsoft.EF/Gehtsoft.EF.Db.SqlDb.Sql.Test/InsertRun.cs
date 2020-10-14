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
    public class InsertRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public InsertRun()
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
        public void SimpleInsert()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);
            List<object> array;

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countBefore = (int)(array[0] as Dictionary<string, object>)["Total"];

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia')"
            );
            result = func(null);
            array = result as List<object>;

            Int64 insertedID = (Int64)(array[0] as Dictionary<string, object>)["LastInsertedId"];
            insertedID.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countAfter = (int)(array[0] as Dictionary<string, object>)["Total"];

            countAfter.Should().Be(countBefore + 1);

            func = environment.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={insertedID}");
            result = func(null);
            array = result as List<object>;
            array.Count().Should().Be(1);

            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            companyName.Should().Be("Gehtsoft");
        }

        [Fact]
        public void InsertFromSelect()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);
            List<object> array;

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countBefore = (int)(array[0] as Dictionary<string, object>)["Total"];

            func = environment.Parse("test", "SELECT * FROM Customer WHERE PostalCode LIKE '80%'");
            result = func(null);
            array = result as List<object>;
            int countShoulfBeAdded = array.Count;
            string shouldBeCompanyName = (string)(array[array.Count - 1] as Dictionary<string, object>)["CompanyName"];

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax) " +
                "SELECT " +
                "CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax " +
                "FROM Customer WHERE PostalCode LIKE '80%'"
            );

            result = func(null);
            array = result as List<object>;

            Int64 lastInsertedID = (Int64)(array[0] as Dictionary<string, object>)["LastInsertedId"];
            lastInsertedID.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countAfter = (int)(array[0] as Dictionary<string, object>)["Total"];

            countAfter.Should().Be(countBefore + countShoulfBeAdded);

            func = environment.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={lastInsertedID}");
            result = func(null);
            array = result as List<object>;
            array.Count().Should().Be(1);

            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            companyName.Should().Be(shouldBeCompanyName);
        }

        [Fact]
        public void InsertParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyNameQQQ, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "(123, 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "(NULL, 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia', '123456789', '123456789', 't.com')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax, HomePage) " +
                    "VALUES " +
                    "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000')"
                )
            );
        }
    }
}

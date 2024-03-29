﻿using FluentAssertions;
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
    public sealed class InsertRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public InsertRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:");
            connection = connectionFactory.GetConnection();
            Snapshot snapshot = new Snapshot();
            snapshot.CreateAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();
            if (connection.ConnectionType == UniversalSqlDbFactory.ORACLE)
            {
                // trick Oracle's 'nextval'
                bool prot = SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries;
                SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;

                int start = 0;
                using (SqlDbQuery q1 = connection.GetQuery("SELECT MAX(supplierID) FROM nw_suppliers"))
                {
                    q1.ExecuteReader();
                    while (q1.ReadNext())
                    {
                        object v = q1.GetValue(0);
                        start = (int)Convert.ChangeType(v, typeof(int));
                    }
                }
                using (SqlDbQuery q1 = connection.GetQuery($"BEGIN \r\nEXECUTE IMMEDIATE 'DROP SEQUENCE nw_suppliers_supplierID';\r\nEXECUTE IMMEDIATE 'CREATE SEQUENCE nw_suppliers_supplierID START WITH {start + 1}';\r\nEND; \r\n"))
                {
                    q1.ExecuteNoData();
                }
                SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = prot;
            }

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
            Func<IDictionary<string, object>, dynamic> func;
            dynamic result;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countBefore = (int)(result[0].Total);

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Omsk', 'Siberia', '644000', 'Russia')"
            );
            result = func(null);

            Int64 insertedID = (Int64)result[0].LastInsertedId;
            insertedID.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countAfter = (int)(result[0].Total);

            countAfter.Should().Be(countBefore + 1);

            func = environment.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={insertedID}");
            result = func(null);
            ((int)result.Count).Should().Be(1);

            string companyName = (string)(result[0].CompanyName);
            companyName.Should().Be("Gehtsoft");
        }

        [Fact]
        public void InsertFromSelect()
        {
            Func<IDictionary<string, object>, dynamic> func;
            dynamic result;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countBefore = (int)(result[0].Total);

            func = environment.Parse("test", "SELECT * FROM Customer WHERE PostalCode LIKE '80%' ORDER BY CustomerID");
            result = func(null);
            int countShoulfBeAdded = result.Count;
            string shouldBeCompanyName = (string)(result[result.Count - 1].CompanyName);

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax) " +
                "SELECT " +
                "CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax " +
                "FROM Customer WHERE PostalCode LIKE '80%'"
            );

            result = func(null);

            Int64 lastInsertedID = (Int64)(result[0].LastInsertedId);
            lastInsertedID.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countAfter = (int)result[0].Total;

            countAfter.Should().Be(countBefore + countShoulfBeAdded);

            func = environment.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={lastInsertedID}");
            result = func(null);
            ((int)result.Count).Should().Be(1);

            string companyName = (string)(result[0].CompanyName);
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
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax) " +
                    "SELECT " +
                    "CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax " +
                    "FROM Customer WHERE PostalCode LIKE '80%' SORT BY CompanyName"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "INSERT INTO Supplier " +
                    "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax) " +
                    "SELECT " +
                    "CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country, Phone, Fax " +
                    "FROM Customer WHERE PostalCode LIKE '80%' GROUP BY CompanyName"
                )
            );
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Utils;
using Microsoft.OData.UriParser;
using Xunit;

namespace Gehtsoft.EF.Test.Northwind
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    [Collection(nameof(NorthwindFixture))]
    public class TestNorthwind
    {
        private readonly NorthwindFixture mFixture;

        public TestNorthwind(NorthwindFixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        [TestOrder(1)]
        public void TablesCreated(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            var schema = (connection as IEntityContext).ExistingTables();

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.CategoryTable.Name,
                                             StringComparison.OrdinalIgnoreCase) &&
                                             table.EntityType == typeof(Category));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.CustomerTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.EmployeeTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.EmployeeTerritoryTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.OrderTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.OrderDetailTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.ProductTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.RegionTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.ShipperTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.SupplierTable.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             mFixture.TerritoryTable.Name,
                                             StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        [TestOrder(2)]
        public void AllDataCreated(string driver)
        {
            var connection = mFixture.GetInstance(driver);

            using (var query = connection.Count<Category>())
                query.GetCount().Should().Be(mFixture.Snapshot.Categories.Count);

            using (var query = connection.Count<Customer>())
                query.GetCount().Should().Be(mFixture.Snapshot.Customers.Count);

            using (var query = connection.Count<Employee>())
                query.GetCount().Should().Be(mFixture.Snapshot.Employees.Count);

            using (var query = connection.Count<EmployeeTerritory>())
                query.GetCount().Should().Be(mFixture.Snapshot.EmployeeTerritories.Count);

            using (var query = connection.Count<Order>())
                query.GetCount().Should().Be(mFixture.Snapshot.Orders.Count);

            using (var query = connection.Count<Product>())
                query.GetCount().Should().Be(mFixture.Snapshot.Products.Count);

            using (var query = connection.Count<Region>())
                query.GetCount().Should().Be(mFixture.Snapshot.Regions.Count);

            using (var query = connection.Count<Shipper>())
                query.GetCount().Should().Be(mFixture.Snapshot.Shippers.Count);
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        [TestOrder(10)]
        public void InsertAndDeleteOrder(string driver)
        {
            var connection = mFixture.GetInstance(driver);

            var order = new Order()
            {
                Customer = connection.Get<Customer>(mFixture.Snapshot.Customers[0].CustomerID),
                Employee = connection.Get<Employee>(mFixture.Snapshot.Employees[0].EmployeeID),
                ShipVia = connection.Get<Shipper>(mFixture.Snapshot.Shippers[0].ShipperID),
                Freight = 123,
                OrderDate = DateTime.Now.AddDays(-1),
                RequiredDate = DateTime.Now.AddDays(10),
                ShipAddress = "address",
                ShipCity = "city",
                ShipCountry = "country",
                ShipName = "name",
                ShippedDate = DateTime.Now,
                ShipPostalCode = "12345",
                ShipRegion = "region"
            };

            using var delete = new DelayedAction(() =>
            {
                if (order.OrderID > 0)
                {
                    using (var query = connection.DeleteEntity<Order>())
                        query.Execute(order);

                    using (var query = connection.Count<Order>())
                    {
                        query.Where.Property(nameof(Order.OrderID)).Eq(order.OrderID);
                        query.GetCount().Should().Be(0);
                    }
                }
            });

            int maxID;

            using (var query = connection.GetGenericSelectEntityQuery<Order>())
            {
                query.AddToResultset(AggFn.Max, nameof(Order.OrderID));
                query.Execute();
                query.ReadNext().Should().BeTrue();

                maxID = query.GetValue<int>(0);
                maxID.Should()
                    .Be(mFixture.Snapshot.Orders.Max(o => o.OrderID))
                    .And.BeGreaterThan(10000);
            }

            connection.Save<Order>(order);
            order.OrderID.Should().BeGreaterThan(maxID);

            using (var query = connection.Count<Order>())
            {
                query.Where.Property(nameof(Order.OrderID)).Eq(order.OrderID);
                query.GetCount().Should().Be(1);
            }
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        [TestOrder(20)]
        public void Select1(string driver)
        {
            var connection = mFixture.GetInstance(driver) as IEntityContext;
            var cats = mFixture.Snapshot.Categories.OrderBy(o => o.CategoryID).ToArray();

            using (var query = connection.Select<Category>())
            {
                query.Order.Add(nameof(Category.CategoryName));
                query.Skip = 2;
                query.Take = 5;

                var cats1 = query.ReadAll<Category>();
                cats1.Should().HaveCount(5);
                cats1[0].CategoryID.Should().Be(cats[2].CategoryID);
                cats1.Should().BeInAscendingOrder(o => o.CategoryID);
            }
        }
    }
}

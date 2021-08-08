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
        [TestOrder(0)]
        public void TablesCreated(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            var schema = connection.ExistingTables();

            schema.Should().Contain(table => table.Name.Equals(
                                             AllEntities.Inst[typeof(Category), true].TableDescriptor.Name,
                                             StringComparison.OrdinalIgnoreCase) &&
                                             table.EntityType == typeof(Category));

            schema.Should().Contain(table => table.Name.Equals(
                                             AllEntities.Inst[typeof(Customer), true].TableDescriptor.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             AllEntities.Inst[typeof(Employee), true].TableDescriptor.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             AllEntities.Inst[typeof(EmployeeTerritory), true].TableDescriptor.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                             AllEntities.Inst[typeof(Order), true].TableDescriptor.Name,
                                             StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                 AllEntities.Inst[typeof(OrderDetail), true].TableDescriptor.Name,
                                 StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                 AllEntities.Inst[typeof(Product), true].TableDescriptor.Name,
                                 StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                 AllEntities.Inst[typeof(Region), true].TableDescriptor.Name,
                                 StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                 AllEntities.Inst[typeof(Shipper), true].TableDescriptor.Name,
                                 StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                 AllEntities.Inst[typeof(Supplier), true].TableDescriptor.Name,
                                 StringComparison.OrdinalIgnoreCase));

            schema.Should().Contain(table => table.Name.Equals(
                                 AllEntities.Inst[typeof(Territory), true].TableDescriptor.Name,
                                 StringComparison.OrdinalIgnoreCase));

        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        [TestOrder(1)]
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
        [TestOrder(0)]
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


    }
}

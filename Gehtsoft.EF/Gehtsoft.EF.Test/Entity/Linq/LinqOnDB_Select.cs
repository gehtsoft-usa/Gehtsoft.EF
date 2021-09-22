using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Linq
{
    [Collection(nameof(NorthwindFixture))]
    public class LinqOnDB_Select
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.SqlConnectionNames(flags, mFlags);

        private readonly NorthwindFixture mFixture;

        public LinqOnDB_Select(NorthwindFixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Count_All(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var products = connection.GetCollectionOf<Product>();

            products.Count().Should().Be(mFixture.Snapshot.Products.Count);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Count_Where(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var products = connection.GetCollectionOf<Product>();

            products.Count(p => p.QuantityPerUnit.Length > 10)
                .Should().Be(mFixture.Snapshot.Products.Count(p => p.QuantityPerUnit.Length > 10));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Max_All(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            orders.Select(o => SqlFunction.Max(o.Quantity)).First()
                .Should().Be(mFixture.Snapshot.OrderDetails.Max(o => o.Quantity));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Avg_All_1(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            orders.Select(o => SqlFunction.Avg(o.Quantity)).First()
                .Should().BeApproximately(mFixture.Snapshot.OrderDetails.Select(o => o.Quantity).Average(), 1e-5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Avg_All_2(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            orders.Select(o => o.Quantity).Average()
                .Should().BeApproximately(mFixture.Snapshot.OrderDetails.Select(o => o.Quantity).Average(), 1e-5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Avg_All_3(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            orders.Average(o => o.Quantity)
                .Should().BeApproximately(mFixture.Snapshot.OrderDetails.Select(o => o.Quantity).Average(), 1e-5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Min_InGroup(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            var totals =
                orders.GroupBy(o => o.Order.OrderID).Select(g => new { Id = g.Key, Quantity = g.Min(v => v.Quantity) }).ToList();

            totals.Count.Should().Be(mFixture.Snapshot.Orders.Count);

            foreach (var total in totals)
            {
                int id = (int)total.Id;
                var q = (double)total.Quantity;

                q.Should().Be(mFixture.Snapshot.OrderDetails.Where(o => o.Order.OrderID == id).Min(o => o.Quantity));
            }

            var first = orders.GroupBy(o => o.Order.OrderID).Select(g => new { Id = g.Key, Quantity = g.Min(v => v.Quantity) }).First();
            first.Should().NotBeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void First_1(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            var order = orders.First();

            order.Should().NotBeNull();
            mFixture.Snapshot.OrderDetails.Should().Contain(o => o.Id == order.Id);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void First_2(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var orders = connection.GetCollectionOf<OrderDetail>();

            var order = orders.OrderBy(o => o.Order.OrderID).First();
            order.Should().NotBeNull();
            mFixture.Snapshot.OrderDetails.Should().Contain(o => o.Id == order.Id);
            order.Order.OrderID.Should().Be(mFixture.Snapshot.Orders.Min(o => o.OrderID));
        }
    }
}

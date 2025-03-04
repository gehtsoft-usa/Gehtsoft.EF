using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Driver;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    [Collection(nameof(NorthwindFixture))]
    public class AdvancedSelectTest
    {
        private readonly NorthwindFixture mFixture;

        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public AdvancedSelectTest(NorthwindFixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AggregateFunction_Count(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.OrderTable);
            select.AddToResultset(AggFn.Count);
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(0).Should().Be(mFixture.Snapshot.Orders.Count);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AggregateFunction_And_Group(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            select.AddToResultset(AggFn.Avg, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select.AddToResultset(AggFn.Max, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select.AddToResultset(AggFn.Min, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select.AddToResultset(AggFn.Sum, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select.AddToResultset(AggFn.Count);

            select.AddGroupBy(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int cc = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var id = query.GetValue<int>(0);
                    var avg = query.GetValue<double>(1);
                    var max = query.GetValue<double>(2);
                    var min = query.GetValue<double>(3);
                    var sum = query.GetValue<double>(4);
                    var count = query.GetValue<int>(5);

                    var set = mFixture.Snapshot.OrderDetails.Where(s => s.Order.OrderID == id).ToList();

                    avg.Should().BeApproximately(set.Average(s => s.Quantity), 1e-6);
                    min.Should().Be(set.Min(s => s.Quantity));
                    max.Should().Be(set.Max(s => s.Quantity));
                    sum.Should().BeApproximately(set.Sum(s => s.Quantity), 1e-6);
                    count.Should().Be(set.Count);
                }

                cc.Should().Be(mFixture.Snapshot.Orders.Count);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AggregateFunction_Sum_Group_Having(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            select.AddToResultset(AggFn.Sum, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select.AddGroupBy(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            select.Having.Property(AggFn.Sum, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]).Gt().Value(100);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int cc = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var id = query.GetValue<int>(0);
                    var s = query.GetValue<double>(1);
                    s.Should().BeApproximately(mFixture.Snapshot.OrderDetails.Where(s => s.Order.OrderID == id).Sum(s => s.Quantity), 1e-6);
                    s.Should().BeGreaterThan(100);
                }

                cc.Should().BeGreaterThan(0);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void LeftJoin_And_Aggregates(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var select = connection.GetSelectQueryBuilder(mFixture.ProductTable);
            var e1 = select.AddTable(mFixture.OrderDetailTable);
            e1.JoinType = TableJoinType.Left;

            select.AddToResultset(mFixture.ProductTable[nameof(Product.ProductID)]);
            select.AddToResultset(mFixture.ProductTable[nameof(Product.ProductName)]);
            select.AddExpressionToResultset(
                connection.GetLanguageSpecifics()
                    .GetSqlFunction(SqlFunctionId.Sum, new string[] {
                            select.GetAlias(mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)], e1) +
                            "*" +
                            select.GetAlias(mFixture.OrderDetailTable[nameof(OrderDetail.UnitPrice)], e1)
                }), DbType.Double, true, "total");

            select.AddGroupBy(mFixture.ProductTable[nameof(Product.ProductID)]);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int cc = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var id = query.GetValue<int>(0);
                    var v = query.GetValue<double?>(2);

                    if (v != null)
                        v.Should().BeApproximately(mFixture.Snapshot.OrderDetails.Where(d => d.Product.ProductID == id).Sum(s => s.UnitPrice * s.Quantity), 1e-6);
                    else
                        mFixture.Snapshot.OrderDetails.Where(d => d.Product.ProductID == id).Should().BeEmpty();
                }

                cc.Should().Be(mFixture.Snapshot.Products.Count);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Query_Connection_NotExists(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var select = connection.GetSelectQueryBuilder(mFixture.ProductTable);

            var subselect = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);

            subselect.Distinct = true;
            subselect.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);

            subselect.Where.Property(mFixture.OrderDetailTable[nameof(OrderDetail.Product)])
                .Eq()
                .Reference(select.GetReference(mFixture.ProductTable[nameof(Product.ProductID)]));

            select.AddToResultset(mFixture.ProductTable[nameof(Product.ProductID)]);
            select.AddToResultset(mFixture.ProductTable[nameof(Product.ProductName)]);

            select.Where.NotExists().Query(subselect);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int cc = 0;

                while (query.ReadNext())
                {
                    cc++;

                    var id = query.GetValue<int>(0);
                    mFixture.Snapshot.OrderDetails.Where(d => d.Product.ProductID == id).Should().BeEmpty();
                }
                cc.Should().BeGreaterThan(1);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OrderBy_Ask(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var select = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select.AddTable(mFixture.OrderTable);

            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Id)]);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)], "qt");
            select.AddToResultset(mFixture.OrderTable[nameof(Order.OrderDate)], "dt");

            select.AddOrderBy(mFixture.OrderTable[nameof(Order.OrderDate)]);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();

                DateTime lastDate = new DateTime(0);

                while (query.ReadNext())
                {
                    var dt = query.GetValue<DateTime>("dt");
                    dt.Should().BeOnOrAfter(lastDate);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void OrderBy_Desc(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var select = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select.AddTable(mFixture.OrderTable);

            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Id)]);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)], "qt");
            select.AddToResultset(mFixture.OrderTable[nameof(Order.OrderDate)], "dt");

            select.AddOrderBy(mFixture.OrderTable[nameof(Order.OrderDate)], SortDir.Desc);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();

                DateTime lastDate = new DateTime(2200, 1, 1);

                while (query.ReadNext())
                {
                    var dt = query.GetValue<DateTime>("dt");
                    dt.Should().BeOnOrBefore(lastDate);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Union(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var select1 = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select1.AddToResultset(mFixture.OrderDetailTable);
            select1.Where.Property(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]).Eq().Parameter("ord1");

            var select2 = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select2.AddToResultset(mFixture.OrderDetailTable);
            select2.Where.Property(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]).Eq().Parameter("ord2");

            var union = connection.GetUnionQueryBuilder(select1);
            union.AddQuery(select2, false);
            union.AddOrderBy(
                union.QueryTableDescriptor[
                        mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)].Name]);

            using (var query = connection.GetQuery(union))
            {
                int ord1 = mFixture.Snapshot.Orders[0].OrderID,
                    ord2 = mFixture.Snapshot.Orders[1].OrderID;

                query.BindParam("ord1", ord1);
                query.BindParam("ord2", ord2);
                query.ExecuteReader();
                int cc = 0;
                double lq = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var oid = query.GetValue<int>(mFixture.OrderDetailType[nameof(OrderDetail.Order)].Name);
                    var q = query.GetValue<double>(mFixture.OrderDetailType[nameof(OrderDetail.Quantity)].Name);

                    if (cc > 1)
                        q.Should().BeGreaterThanOrEqualTo(lq);

                    (oid == mFixture.Snapshot.Orders[0].OrderID ||
                     oid == mFixture.Snapshot.Orders[1].OrderID).Should().BeTrue();

                    lq = q;
                }
                cc.Should().BeGreaterThan(0);
                cc.Should().Be(mFixture.Snapshot.OrderDetails.Count(r => r.Order.OrderID == ord1 || r.Order.OrderID == ord2));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Skip(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var data = mFixture.Snapshot.Categories.OrderBy(c => c.CategoryName).ToArray();

            connection.GetLanguageSpecifics().SupportsPaging.Should().NotBe(SqlDbLanguageSpecifics.PagingSupport.None);

            var select = connection.GetSelectQueryBuilder(mFixture.CategoryTable);
            select.AddToResultset(mFixture.CategoryTable[nameof(Category.CategoryID)]);
            select.AddOrderBy(mFixture.CategoryTable[nameof(Category.CategoryName)]);
            select.Skip = 2;

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int i = 0;
                while (query.ReadNext())
                {
                    var id = query.GetValue<int>(0);
                    id.Should().Be(data[i + 2].CategoryID);
                    i++;
                }
                i.Should().Be(data.Length - 2);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Take(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var data = mFixture.Snapshot.Categories.OrderBy(c => c.CategoryName).ToArray();

            connection.GetLanguageSpecifics().SupportsPaging.Should().NotBe(SqlDbLanguageSpecifics.PagingSupport.None);

            var select = connection.GetSelectQueryBuilder(mFixture.CategoryTable);
            select.AddToResultset(mFixture.CategoryTable[nameof(Category.CategoryID)]);
            select.AddOrderBy(mFixture.CategoryTable[nameof(Category.CategoryName)]);
            select.Limit = 2;

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int i = 0;
                while (query.ReadNext())
                {
                    var id = query.GetValue<int>(0);
                    id.Should().Be(data[i].CategoryID);
                    i++;
                }
                i.Should().Be(2);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SkipTake(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var data = mFixture.Snapshot.Categories.OrderBy(c => c.CategoryName).ToArray();

            connection.GetLanguageSpecifics().SupportsPaging.Should().NotBe(SqlDbLanguageSpecifics.PagingSupport.None);

            var select = connection.GetSelectQueryBuilder(mFixture.CategoryTable);
            select.AddToResultset(mFixture.CategoryTable[nameof(Category.CategoryID)]);
            select.AddOrderBy(mFixture.CategoryTable[nameof(Category.CategoryName)]);
            select.Skip = 1;
            select.Limit = 2;

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int i = 0;
                while (query.ReadNext())
                {
                    var id = query.GetValue<int>(0);
                    id.Should().Be(data[i + 1].CategoryID);
                    i++;
                }
                i.Should().Be(2);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void QueryInResultset(string connectionName)
        {
            //select order and the maximum size of the order position
            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.OrderTable);
            select.AddToResultset(mFixture.OrderTable[nameof(Order.OrderID)]);

            var referenceOrder = select.GetReference(mFixture.OrderTable[nameof(Order.OrderID)]);

            var select1 = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select1.AddToResultset(AggFn.Max, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select1.Where.Property(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]).Eq().Reference(referenceOrder);

            select.AddToResultset(select1, DbType.Double, "maxQuantity");

            select.PrepareQuery();

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int cc = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var id = query.GetValue<int>(0);
                    var maxQuantity = query.GetValue<double>(1);

                    maxQuantity.Should()
                        .BeApproximately(mFixture.Snapshot.OrderDetails.Where(o => o.Order.OrderID == id)
                                                                       .Max(o => o.Quantity), 1e-7);
                }
                cc.Should().Be(mFixture.Snapshot.Orders.Count);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void QueryInWhere(string connectionName)
        {
            //select order and the minimum size of the order position
            var connection = mFixture.GetInstance(connectionName);

            var select = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select.Distinct = true; //there could be more than 1 position with minimum amount in the order
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            select.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);

            var referenceOrder = select.GetReference(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);
            var select1 = connection.GetSelectQueryBuilder(mFixture.OrderDetailTable);
            select1.AddToResultset(AggFn.Min, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]);
            select1.Where.Property(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]).Eq().Reference(referenceOrder);

            select.Where.Property(mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)]).Eq().Query(select1);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                int cc = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var id = query.GetValue<int>(0);
                    var maxQuantity = query.GetValue<double>(1);

                    maxQuantity.Should()
                        .BeApproximately(mFixture.Snapshot.OrderDetails.Where(o => o.Order.OrderID == id)
                                                                       .Min(o => o.Quantity), 1e-7);
                }
                cc.Should().Be(mFixture.Snapshot.Orders.Count);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Pivot(string connectionName)
        {
            //selects total for each order (in rows) and category (in columns)
            //the solution is not really optimal and rather is designed to demonstrated
            //a complex case of handling the query.

            var connection = mFixture.GetInstance(connectionName);

            var pivotSelect = connection.GetSelectQueryBuilder(mFixture.OrderTable);
            pivotSelect.AddToResultset(mFixture.OrderTable[nameof(Order.OrderID)], "orderid");

            //source select - find total per category per order
            var sourceSelect = connection.GetSelectQueryBuilder(mFixture.CategoryTable);
            var j1 = sourceSelect.AddTable(mFixture.ProductTable);
            j1.JoinType = TableJoinType.Left;
            var j2 = sourceSelect.AddTable(mFixture.OrderDetailTable);
            j2.JoinType = TableJoinType.Left;
            sourceSelect.AddToResultset(mFixture.CategoryTable[nameof(Category.CategoryID)], "category");
            sourceSelect.AddToResultset(mFixture.OrderDetailTable[nameof(OrderDetail.Order)], "orderid");
            sourceSelect.AddToResultset(AggFn.Sum, mFixture.OrderDetailTable[nameof(OrderDetail.Quantity)], "total");
            sourceSelect.AddGroupBy(mFixture.CategoryTable[nameof(Category.CategoryID)]);
            sourceSelect.AddGroupBy(mFixture.OrderDetailTable[nameof(OrderDetail.Order)]);

            //pivot select from the source select
            for (int i = 0; i < mFixture.Snapshot.Categories.Count; i++)
            {
                var j = pivotSelect.AddTable(sourceSelect.QueryTableDescriptor, false);
                j.JoinType = TableJoinType.Left;

                j.On.Property(sourceSelect.QueryTableDescriptor["orderid"], j).Eq().Property(mFixture.OrderTable[nameof(Order.OrderID)])
                    .And().Property(sourceSelect.QueryTableDescriptor["category"], j).Eq().Value(mFixture.Snapshot.Categories[i].CategoryID);

                pivotSelect.AddToResultset(sourceSelect.QueryTableDescriptor["total"], j, $"cat{mFixture.Snapshot.Categories[i].CategoryID}");
            }

            EntityCollection<OrderDetail> orderDetails;
            using (var query = connection.GetSelectEntitiesQuery<OrderDetail>())
                orderDetails = query.ReadAll<OrderDetail>();

            using (var query = connection.GetQuery(pivotSelect))
            {
                query.ExecuteReader();
                int cc = 0;
                while (query.ReadNext())
                {
                    cc++;
                    var order = query.GetValue<int>(0);
                    for (int i = 0; i < mFixture.Snapshot.Categories.Count; i++)
                    {
                        var total = query.GetValue<double>(i + 1);
                        total.Should()
                            .BeApproximately(orderDetails.Where(o => o.Order.OrderID == order &&
                                                                     o.Product.Category.CategoryID == mFixture.Snapshot.Categories[i].CategoryID)
                                                         .Sum(o => o.Quantity), 1e-7);
                    }
                }
                cc.Should().Be(mFixture.Snapshot.Orders.Count);
            }
        }
    }
}



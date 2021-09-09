using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class QueryiesOnDb_AdvancedSelect : IClassFixture<NorthwindFixture>
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.ConnectionNames(flags, mFlags);

        public NorthwindFixture mFixture;

        public QueryiesOnDb_AdvancedSelect(NorthwindFixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_OnlyChosenFields_SpecifyDirectly(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetSelectEntitiesQueryBase<Employee>())
            {
                var td = AllEntities.Get<Employee>().TableDescriptor;
                var e0 = mFixture.Snapshot.Employees[0];

                query.AddToResultset(nameof(Employee.EmployeeID));
                query.AddToResultset(nameof(Employee.FirstName));
                query.AddToResultset(nameof(Employee.LastName));

                query.Where.Property(nameof(Employee.EmployeeID)).Eq().Value(e0.EmployeeID);

                query.Execute();
                query.FieldCount.Should().Be(3);
                query.Field(0)
                    .Name.ToUpper().Should().EndWith(td[nameof(Employee.EmployeeID)].Name.ToUpper());
                query.Field(1)
                    .Name.ToUpper().Should().EndWith(td[nameof(Employee.FirstName)].Name.ToUpper());
                query.Field(2)
                    .Name.ToUpper().Should().EndWith(td[nameof(Employee.LastName)].Name.ToUpper());

                query.ReadNext().Should().BeTrue();

                query.GetValue<int>(0).Should().Be(e0.EmployeeID);
                query.GetValue<string>(1).Should().Be(e0.FirstName);
                query.GetValue<string>(2).Should().Be(e0.LastName);

                query.ReadNext().Should().BeFalse();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_AggFn_Count(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetSelectEntitiesQueryBase<OrderDetail>())
            {
                query.AddToResultset(nameof(OrderDetail.Order), "ordid");
                query.AddToResultset(AggFn.Count, null, "ordcnt");
                query.AddGroupBy(nameof(OrderDetail.Order));

                var all = query.ReadAllDynamic();
                foreach (var a in all)
                {
                    int id = (int)a.ordid;
                    int count = (int)a.ordcnt;

                    count.Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Order.OrderID == id));
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_AggFn_Sum_TwoLevels(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetSelectEntitiesQueryBase<OrderDetail>())
            {
                query.AddEntity<Product>();
                query.AddEntity<Order>();

                query.AddToResultset(typeof(Product), nameof(Product.ProductID), "pid");
                query.AddToResultset(typeof(Order), nameof(Order.OrderDate), "dt");
                query.AddToResultset(AggFn.Sum, nameof(OrderDetail.Quantity), "qty");
                query.AddGroupBy(typeof(Product), nameof(Product.ProductID));
                query.AddGroupBy(typeof(Order), nameof(Order.OrderDate));

                var ss = mFixture.Snapshot;

                var all = query.ReadAllDynamic();
                foreach (var a in all)
                {
                    int pid = (int)a.pid;
                    DateTime dt = (DateTime)a.dt;
                    double count = (double)a.qty;

                    count.Should().Be(ss.OrderDetails.Where(
                        o => ss.Orders.First(o1 => o1.OrderID == o.Order.OrderID).OrderDate == dt &&
                             o.Product.ProductID == pid).Sum(o => o.Quantity));
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_OnlyChosenFields_ViaFilter(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using (var query = connection.GetSelectEntitiesQuery<Employee>
                (new[]
                {
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.Title) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.TitleOfCourtesy) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.BirthDate) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.HireDate) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.Address) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.City) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.Region) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.PostalCode) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.Country) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.HomePhone) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.Notes) },
                    new SelectEntityQueryFilter() { EntityType = typeof(Employee), Property = nameof(Employee.ReportsTo) },
                }))
            {
                var td = AllEntities.Get<Employee>().TableDescriptor;
                var e0 = mFixture.Snapshot.Employees[0];

                query.Where.Property(nameof(Employee.EmployeeID)).Eq().Value(e0.EmployeeID);

                query.Execute();
                query.FieldCount.Should().Be(3);
                query.Field(0)
                    .Name.ToUpper().Should().EndWith(td[nameof(Employee.EmployeeID)].Name.ToUpper());
                query.Field(1)
                    .Name.ToUpper().Should().EndWith(td[nameof(Employee.LastName)].Name.ToUpper());
                query.Field(2)
                    .Name.ToUpper().Should().EndWith(td[nameof(Employee.FirstName)].Name.ToUpper());

                var e = query.ReadOne() as Employee;
                e.Should().NotBeNull();

                query.GetValue<int>(0).Should().Be(e0.EmployeeID);
                query.GetValue<string>(1).Should().Be(e0.LastName);
                query.GetValue<string>(2).Should().Be(e0.FirstName);

                e.EmployeeID.Should().Be(e0.EmployeeID);
                e.LastName.Should().Be(e0.LastName);
                e.FirstName.Should().Be(e0.FirstName);

                query.ReadNext().Should().BeFalse();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_ReadEntityWithDependencies(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using (var query = connection.GetSelectEntitiesQuery<OrderDetail>())
            {
                var d0 = mFixture.Snapshot.OrderDetails[0];
                var o0 = mFixture.Snapshot.Orders.FirstOrDefault(o => o.OrderID == d0.Order.OrderID);
                var p0 = mFixture.Snapshot.Products.FirstOrDefault(p => p.ProductID == d0.Product.ProductID);
                var c0 = mFixture.Snapshot.Categories.FirstOrDefault(c => c.CategoryID == p0.Category.CategoryID);
                var s0 = mFixture.Snapshot.Suppliers.FirstOrDefault(c => c.SupplierID == p0.Supplier.SupplierID);

                query.Where.Property(nameof(OrderDetail.Id)).Eq().Value(d0.Id);

                query.Execute();
                var d = query.ReadOne<OrderDetail>();

                //own field
                d.UnitPrice.Should().Be(d0.UnitPrice);
                d.Quantity.Should().Be(d0.Quantity);
                d.Discount.Should().Be(d0.Discount);

                d.Order.Should().NotBeNull();

                var o = d.Order;
                o.Should().NotBeNull();
                o.OrderID.Should().Be(o0.OrderID);
                o.OrderDate.Should().Be(o0.OrderDate);
                o.RequiredDate.Should().Be(o0.RequiredDate);
                o.ShippedDate.Should().Be(o0.ShippedDate);
                o.ShipName.Should().Be(o0.ShipName);
                o.ShipCity.Should().Be(o0.ShipCity);
                o.ShipRegion.Should().Be(o0.ShipRegion);
                o.ShipPostalCode.Should().Be(o0.ShipPostalCode);

                //dictionary decode
                var p = d.Product;
                p.Should().NotBeNull();

                p.ProductID.Should().Be(p0.ProductID);
                p.ProductName.Should().Be(p0.ProductName);
                p.QuantityPerUnit.Should().Be(p0.QuantityPerUnit);
                p.ReorderLevel.Should().Be(p0.ReorderLevel);
                p.UnitPrice.Should().Be(p0.UnitPrice);
                p.UnitsInStock.Should().Be(p0.UnitsInStock);
                p.UnitsOnOrder.Should().Be(p0.UnitsOnOrder);

                p.Category.Should().NotBeNull();
                p.Category.CategoryID.Should().Be(c0.CategoryID);
                p.Category.CategoryName.Should().Be(c0.CategoryName);

                p.Supplier.Should().NotBeNull();
                p.Supplier.SupplierID.Should().Be(s0.SupplierID);
                p.Supplier.CompanyName.Should().Be(s0.CompanyName);
                p.Supplier.Address.Should().Be(s0.Address);
                p.Supplier.City.Should().Be(s0.City);
                p.Supplier.Country.Should().Be(s0.Country);
                p.Supplier.ContactName.Should().Be(s0.ContactName);
                p.Supplier.ContactTitle.Should().Be(s0.ContactTitle);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_SelfConnected(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using (var query = connection.GetSelectEntitiesQuery<Employee>())
            {
                var e0 = mFixture.Snapshot.Employees.FirstOrDefault(e => e.ReportsTo != null && mFixture.Snapshot.Employees.Any(e1 => e1.EmployeeID == e.ReportsTo.EmployeeID && e1.ReportsTo != null));
                var e1 = mFixture.Snapshot.Employees.FirstOrDefault(e => e.EmployeeID == e0.ReportsTo.EmployeeID);

                query.Where.Property(nameof(Employee.EmployeeID)).Eq().Value(e0.EmployeeID);

                query.Execute();
                var e = query.ReadOne<Employee>();

                e.ReportsTo.Should().NotBeNull();
                e.ReportsTo.EmployeeID.Should().Be(e0.ReportsTo.EmployeeID);
                e.ReportsTo.LastName.Should().Be(e1.LastName);
                e.ReportsTo.ReportsTo.Should().NotBeNull();
                e.ReportsTo.ReportsTo.EmployeeID.Should().Be(e1.ReportsTo.EmployeeID);
                e.ReportsTo.ReportsTo.LastName.Should().BeNull(because: "we don't connect to 2nd level of self-connections");
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_Function(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetSelectEntitiesQueryBase<Category>())
            {
                query.AddEntity<Product>();

                var r1 = query.GetReference(nameof(Category.CategoryName));
                var r2 = query.GetReference(typeof(Product), nameof(Product.ProductName));
                var ls = connection.GetLanguageSpecifics();
                query.AddToResultset(typeof(Product), nameof(Product.ProductID), "pid");
                query.AddExpressionToResultset(
                    ls.GetSqlFunction(SqlFunctionId.Concat,
                        new string[]
                        {
                            ls.GetSqlFunction(SqlFunctionId.Left, new [] { r1.Alias, "2" }),
                            ls.GetSqlFunction(SqlFunctionId.Left, new [] { r2.Alias, "3" })
                        }
                    ), DbType.String, "n");

                var ss = mFixture.Snapshot;

                var all = query.ReadAllDynamic();

                foreach (var a in all)
                {
                    int id = (int)a.pid;
                    string n = (string)a.n;

                    var p = ss.Products.First(p => p.ProductID == id);
                    var c = ss.Categories.First(c => c.CategoryID == p.Category.CategoryID);

                    n.Should().Be(c.CategoryName.Substring(0, 2) + p.ProductName.Substring(0, 3));
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Resultset_Query(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var subquery = connection.GetSelectEntitiesQueryBase<Product>())
            using (var query = connection.GetSelectEntitiesQueryBase<Category>())
            {
                var r1 = query.GetReference(nameof(Category.CategoryID));

                subquery.AddToResultset(AggFn.Count, null);
                subquery.Where.Property(nameof(Product.Category)).Eq().Reference(r1);

                query.AddToResultset(nameof(Category.CategoryID), "cid");
                query.AddToResultset(subquery, typeof(double), "ccnt");

                var ss = mFixture.Snapshot;
                var all = query.ReadAllDynamic();
                foreach (var a in all)
                {
                    int cid = (int)a.cid;
                    double ccnt = (double)a.ccnt;

                    ccnt.Should().Be(ss.Products.Count(p => p.Category.CategoryID == cid));
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Function(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetSelectEntitiesQuery<Product>())
            {
                query.Where.Property(nameof(Product.ProductName)).Left(1).ToUpper().Gt("C");
                var ps = query.ReadAll<Product>();

                ps.Should().HaveCount(mFixture.Snapshot.Products.Count(p => p.ProductName[0] > 'C'));
                ps.Count.Should().BeGreaterThan(0);
                ps.Should().HaveAllElementsMatching(p => p.ProductName[0] > 'C');
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Where_Query(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var ss = mFixture.Snapshot;

            int threshold = 5;

            ss.Categories.Any(c => ss.Products.Count(p => p.Category.CategoryID == c.CategoryID) <= threshold).Should().BeTrue();
            ss.Categories.Any(c => ss.Products.Count(p => p.Category.CategoryID == c.CategoryID) > threshold).Should().BeTrue();

            using (var query = connection.GetSelectEntitiesQuery<Category>())
            using (var subquery = connection.GetSelectEntitiesCountQuery<Product>())
            {
                var r = query.GetReference(nameof(Category.CategoryID));
                subquery.Where.Property(nameof(Product.Category)).Eq().Reference(r);

                query.Where.And().Query(subquery).Gt(threshold);
                var cs = query.ReadAll<Category>();
                cs.Count.Should().BeGreaterThan(0);
                cs.Should().HaveAllElementsMatching(c => ss.Products.Count(p => p.Category.CategoryID == c.CategoryID) > threshold);
            }
        }
    }
}


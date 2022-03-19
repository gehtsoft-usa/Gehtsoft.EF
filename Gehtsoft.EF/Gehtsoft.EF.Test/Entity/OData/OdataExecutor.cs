using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.OData;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

#pragma warning disable RCS1049 // Simplify boolean comparison.

namespace Gehtsoft.EF.Test.Entity.OData
{
    public class OdataExecutor : IClassFixture<OdataExecutor.Fixture>
    {
        public sealed class Fixture : IDisposable
        {
            private readonly NorthwindFixture mNorthwind;

            public Snapshot Snapshot => mNorthwind.Snapshot;
            public SqlDbConnection Connection { get; }
            public ODataProcessor Processor { get; }
            public EdmModelBuilder Builder { get; }

            public Fixture()
            {
                var config = AppConfiguration.Instance.GetSqlConnection("sqlite-memory");
                Connection = UniversalSqlDbFactory.Create(config.Driver, config.ConnectionString);
                mNorthwind = new NorthwindFixture();
                mNorthwind.CreateSnapshot(Connection);

                EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Order).Assembly }, "northwind", false);
                EdmModelBuilder builder = new EdmModelBuilder();
                builder.Build(entities, "northwind");

                Builder = builder;
                Processor = new ODataProcessor(new ExistingConnectionFactory(Connection), builder, "");
            }

            public void Dispose()
            {
                Connection.Dispose();
            }
        }

        private readonly Fixture mFixture;

        public OdataExecutor(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Fact]
        public void SelectEntitiesCount_All()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Employee/$count", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(long));
            result.Should().Be(mFixture.Snapshot.Employees.Count);
        }

        [Fact]
        public void SelectEntitiesCount_ByPath()
        {
            object result = mFixture.Processor.SelectData(new Uri($"/Order({mFixture.Snapshot.Orders[1].OrderID})/OrderDetail/$count", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(long));
            result.Should().Be(mFixture.Snapshot.OrderDetails.Count(od => od.Order.OrderID == mFixture.Snapshot.Orders[1].OrderID));
        }

        [Fact]
        public void SelectEntitiesCount_ByContition_EntityProperty()
        {
            object result = mFixture.Processor.SelectData(new Uri("/OrderDetail/$count?$filter=Quantity gt 20", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(long));
            result.Should().Be(mFixture.Snapshot.OrderDetails.Count(od => od.Quantity > 20));
        }

        [Fact]
        public void SelectEntitiesCount_ByContition_DictionaryID()
        {
            var product = mFixture.Snapshot.Products[0];
            object result = mFixture.Processor.SelectData(new Uri($"/OrderDetail/$count?$filter=Product/ProductID eq {product.ProductID}", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(long));
            result.Should().Be(mFixture.Snapshot.OrderDetails.Count(od => od.Product.ProductID == product.ProductID));
        }

        [Fact]
        public void SelectEntitiesCount_ByContition_DictionaryOfDictionaryWithCondition()
        {
            var cat = mFixture.Snapshot.Categories[0];
            var products = mFixture.Snapshot.Products.Where(p => p.Category.CategoryID == cat.CategoryID).ToArray();
            object result = mFixture.Processor.SelectData(new Uri($"/OrderDetail/$count?$expand=Product($filter=Category/CategoryID eq {cat.CategoryID})&$filter=Quantity gt 10", UriKind.Relative));
            result.Should().NotBeNull();
            result.Should().BeOfType(typeof(long));
            result.Should().Be(mFixture.Snapshot.OrderDetails.Count(od => od.Quantity > 10 && products.Any(p => p.ProductID == od.Product.ProductID)));
        }

        [Fact]
        public void SelectOne_ByID()
        {
            var product = mFixture.Snapshot.Products[0];
            object result = mFixture.Processor.SelectData(new Uri($"/Product({product.ProductID})", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElement("ProductID", product.ProductID)
                .And.HaveElement("ProductName", product.ProductName)
                .And.HaveElement("ReorderLevel", product.ReorderLevel)
                .And.HaveElement("Discontinued", product.Discontinued)
                .And.HaveElement("UnitPrice", product.UnitPrice)
                .And.HaveElement("UnitsInStock", product.UnitsInStock)
                .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit)
                .And.HaveElement("categoryID", product.Category.CategoryID)
                .And.HaveElement("supplierID", product.Supplier.SupplierID);
        }

        [Fact]
        public void SelectOne_ByID_Expand_Dictionary()
        {
            var product = mFixture.Snapshot.Products[0];
            object result = mFixture.Processor.SelectData(new Uri($"/Product({product.ProductID})?$expand=Category,Supplier", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElement("ProductID", product.ProductID)
                .And.HaveElement("ProductName", product.ProductName)
                .And.HaveElement("ReorderLevel", product.ReorderLevel)
                .And.HaveElement("Discontinued", product.Discontinued)
                .And.HaveElement("UnitPrice", product.UnitPrice)
                .And.HaveElement("UnitsInStock", product.UnitsInStock)
                .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit)
                .And.HaveElementMatching("Category", v => v is IDictionary<string, object>)
                .And.HaveElementMatching("Supplier", v => v is IDictionary<string, object>);

            var cat = result.As<IDictionary<string, object>>()["Category"].As<IDictionary<string, object>>();
            var cat1 = mFixture.Snapshot.Categories.First(c => c.CategoryID == product.Category.CategoryID);

            cat.Should()
                .HaveElement("CategoryID", cat1.CategoryID)
                .And.HaveElement("CategoryName", cat1.CategoryName);

            var supplier = result.As<IDictionary<string, object>>()["Supplier"].As<IDictionary<string, object>>();
            var supplier1 = mFixture.Snapshot.Suppliers.First(c => c.SupplierID == product.Supplier.SupplierID);

            supplier.Should()
                .HaveElement("SupplierID", supplier1.SupplierID)
                .And.HaveElement("CompanyName", supplier1.CompanyName)
                .And.HaveElement("ContactName", supplier1.ContactName)
                .And.HaveElement("ContactTitle", supplier1.ContactTitle)
                .And.HaveElement("Country", supplier1.Country)
                .And.HaveElement("City", supplier1.City)
                .And.HaveElement("Phone", supplier1.Phone)
                .And.HaveElement("Fax", supplier1.Fax)
                .And.HaveElement("Address", supplier1.Address)
                .And.HaveElement("Phone", supplier1.Phone)
                .And.HaveElement("PostalCode", supplier1.PostalCode)
                .And.HaveElement("Region", supplier1.Region);
        }

        [Fact]
        public void SelectOne_ByID_Expand_Dictionary_DefineResultset()
        {
            var product = mFixture.Snapshot.Products[0];
            object result = mFixture.Processor.SelectData(new Uri($"/Product({product.ProductID})?$expand=Category,Supplier($select=SupplierID,CompanyName,ContactName,Phone)&$select=ProductID,ProductName", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElement("ProductID", product.ProductID)
                .And.HaveElement("ProductName", product.ProductName)
                .And.HaveNoElement("ReorderLevel")
                .And.HaveNoElement("Discontinued")
                .And.HaveNoElement("UnitPrice")
                .And.HaveNoElement("UnitsInStock")
                .And.HaveNoElement("UnitsOnOrder")
                .And.HaveNoElement("QuantityPerUnit")
                .And.HaveElementMatching("Category", v => v is IDictionary<string, object>)
                .And.HaveElementMatching("Supplier", v => v is IDictionary<string, object>);

            var cat = result.As<IDictionary<string, object>>()["Category"].As<IDictionary<string, object>>();
            var cat1 = mFixture.Snapshot.Categories.First(c => c.CategoryID == product.Category.CategoryID);

            cat.Should()
                .HaveElement("CategoryID", cat1.CategoryID)
                .And.HaveElement("CategoryName", cat1.CategoryName);

            var supplier = result.As<IDictionary<string, object>>()["Supplier"].As<IDictionary<string, object>>();
            var supplier1 = mFixture.Snapshot.Suppliers.First(c => c.SupplierID == product.Supplier.SupplierID);

            supplier.Should()
                .HaveElement("SupplierID", supplier1.SupplierID)
                .And.HaveElement("CompanyName", supplier1.CompanyName)
                .And.HaveElement("ContactName", supplier1.ContactName)
                .And.HaveNoElement("ContactTitle")
                .And.HaveNoElement("Country")
                .And.HaveNoElement("City")
                .And.HaveElement("Phone", supplier1.Phone)
                .And.HaveNoElement("Fax")
                .And.HaveNoElement("Address")
                .And.HaveNoElement("PostalCode")
                .And.HaveNoElement("Region");
        }

        [Fact]
        public void SelectAll()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["value"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(mFixture.Snapshot.Products.Count);
            var pid = new HashSet<int>();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        if (pid.Contains((int)v))
                            return false;
                        pid.Add((int)v);
                        product = mFixture.Snapshot.Products.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });
                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveElement("ReorderLevel", product.ReorderLevel)
                    .And.HaveElement("Discontinued", product.Discontinued)
                    .And.HaveElement("UnitPrice", product.UnitPrice)
                    .And.HaveElement("UnitsInStock", product.UnitsInStock)
                    .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                    .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit)
                    .And.HaveElement("categoryID", product.Category.CategoryID)
                    .And.HaveElement("supplierID", product.Supplier.SupplierID);
            }

            pid.Should().HaveCount(mFixture.Snapshot.Products.Count);
        }

        [Fact]
        public void SelectAll_Expand_Dictionary()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product?$expand=Category,Supplier", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["value"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(mFixture.Snapshot.Products.Count);
            var pid = new HashSet<int>();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        if (pid.Contains((int)v))
                            return false;
                        pid.Add((int)v);
                        product = mFixture.Snapshot.Products.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });
                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveElement("ReorderLevel", product.ReorderLevel)
                    .And.HaveElement("Discontinued", product.Discontinued)
                    .And.HaveElement("UnitPrice", product.UnitPrice)
                    .And.HaveElement("UnitsInStock", product.UnitsInStock)
                    .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                    .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit);

                var cat = element.As<IDictionary<string, object>>()["Category"].As<IDictionary<string, object>>();
                var cat1 = mFixture.Snapshot.Categories.First(c => c.CategoryID == product.Category.CategoryID);

                cat.Should()
                    .HaveElement("CategoryID", cat1.CategoryID)
                    .And.HaveElement("CategoryName", cat1.CategoryName);

                var supplier = element.As<IDictionary<string, object>>()["Supplier"].As<IDictionary<string, object>>();
                var supplier1 = mFixture.Snapshot.Suppliers.First(c => c.SupplierID == product.Supplier.SupplierID);

                supplier.Should()
                    .HaveElement("SupplierID", supplier1.SupplierID)
                    .And.HaveElement("CompanyName", supplier1.CompanyName)
                    .And.HaveElement("ContactTitle", supplier1.ContactTitle)
                    .And.HaveElement("ContactName", supplier1.ContactName)
                    .And.HaveElement("Country", supplier1.Country)
                    .And.HaveElement("City", supplier1.City)
                    .And.HaveElement("Phone", supplier1.Phone)
                    .And.HaveElement("Fax", supplier1.Fax)
                    .And.HaveElement("Address", supplier1.Address)
                    .And.HaveElement("PostalCode", supplier1.PostalCode)
                    .And.HaveElement("Region", supplier1.Region);
            }

            pid.Should().HaveCount(mFixture.Snapshot.Products.Count);
        }

        [Fact]
        public void SelectAll_One_To_Many_1()
        {
            var category = mFixture.Snapshot.Categories[0];
            var products = mFixture.Snapshot.Products.Where(p => p.Category.CategoryID == category.CategoryID).ToArray();

            products.Should().NotBeEmpty();

            object result = mFixture.Processor.SelectData(new Uri($"/Category({category.CategoryID})/Product?$expand=Category", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();

            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["value"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(products.Length);
            var pid = new HashSet<int>();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        if (pid.Contains((int)v))
                            return false;
                        pid.Add((int)v);
                        product = products.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });

                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveElement("ReorderLevel", product.ReorderLevel)
                    .And.HaveElement("Discontinued", product.Discontinued)
                    .And.HaveElement("UnitPrice", product.UnitPrice)
                    .And.HaveElement("UnitsInStock", product.UnitsInStock)
                    .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                    .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit);

                var cat = element.As<IDictionary<string, object>>()["Category"].As<IDictionary<string, object>>();

                cat.Should()
                    .HaveElement("CategoryID", category.CategoryID)
                    .And.HaveElement("CategoryName", category.CategoryName);
            }
            pid.Should().HaveCount(products.Length);
        }

        [Fact]
        public void SelectAll_One_To_Many_2()
        {
            var category = mFixture.Snapshot.Categories[0];
            var products = mFixture.Snapshot.Products.Where(p => p.Category.CategoryID == category.CategoryID).ToArray();

            products.Should().NotBeEmpty();

            object result = mFixture.Processor.SelectData(new Uri($"/Category({category.CategoryID})?$expand=Product", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();

            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("Product", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["Product"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(products.Length);
            var pid = new HashSet<int>();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        if (pid.Contains((int)v))
                            return false;
                        pid.Add((int)v);
                        product = products.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });

                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveElement("ReorderLevel", product.ReorderLevel)
                    .And.HaveElement("Discontinued", product.Discontinued)
                    .And.HaveElement("UnitPrice", product.UnitPrice)
                    .And.HaveElement("UnitsInStock", product.UnitsInStock)
                    .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                    .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit);
            }
            pid.Should().HaveCount(products.Length);
        }

        [Fact]
        public void SelectAll_One_To_Many_3()
        {
            var category = mFixture.Snapshot.Categories[0];
            var products = mFixture.Snapshot.Products.Where(p => p.Category.CategoryID == category.CategoryID).ToArray();

            products.Should().NotBeEmpty();

            object result = mFixture.Processor.SelectData(new Uri($"/Category?$filter=CategoryID eq {category.CategoryID}&$expand=Product", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();

            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["value"].As<IReadOnlyCollection<object>>();
            collection.Should().HaveCount(1);

            var category1 = collection.First().As<IDictionary<string, object>>();

            category1.Should()
                .HaveElementMatching("CategoryID", v => v is int i && i == category.CategoryID)
                .And.HaveElementMatching("Product", v => v is IReadOnlyCollection<object>);

            collection = category1.As<IDictionary<string, object>>()["Product"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(products.Length);
            var pid = new HashSet<int>();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        if (pid.Contains((int)v))
                            return false;
                        pid.Add((int)v);
                        product = products.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });

                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveElement("ReorderLevel", product.ReorderLevel)
                    .And.HaveElement("Discontinued", product.Discontinued)
                    .And.HaveElement("UnitPrice", product.UnitPrice)
                    .And.HaveElement("UnitsInStock", product.UnitsInStock)
                    .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                    .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit);
            }
            pid.Should().HaveCount(products.Length);
        }

        [Fact]
        public void SelectAll_Expand_Dictionary_DefineResultset()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product?$expand=Category,Supplier($select=SupplierID,CompanyName,ContactName,Phone)&$select=ProductID,ProductName", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["value"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(mFixture.Snapshot.Products.Count);
            var pid = new HashSet<int>();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        if (pid.Contains((int)v))
                            return false;
                        pid.Add((int)v);
                        product = mFixture.Snapshot.Products.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });

                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveNoElement("ReorderLevel")
                    .And.HaveNoElement("Discontinued")
                    .And.HaveNoElement("UnitPrice")
                    .And.HaveNoElement("UnitsInStock")
                    .And.HaveNoElement("UnitsOnOrder")
                    .And.HaveNoElement("QuantityPerUnit");

                var cat = element.As<IDictionary<string, object>>()["Category"].As<IDictionary<string, object>>();
                var cat1 = mFixture.Snapshot.Categories.First(c => c.CategoryID == product.Category.CategoryID);

                cat.Should()
                    .HaveElement("CategoryID", cat1.CategoryID)
                    .And.HaveElement("CategoryName", cat1.CategoryName);

                var supplier = element.As<IDictionary<string, object>>()["Supplier"].As<IDictionary<string, object>>();
                var supplier1 = mFixture.Snapshot.Suppliers.First(c => c.SupplierID == product.Supplier.SupplierID);

                supplier.Should()
                 .HaveElement("SupplierID", supplier1.SupplierID)
                 .And.HaveElement("CompanyName", supplier1.CompanyName)
                 .And.HaveElement("ContactName", supplier1.ContactName)
                 .And.HaveNoElement("ContactTitle")
                 .And.HaveNoElement("Country")
                 .And.HaveNoElement("City")
                 .And.HaveElement("Phone", supplier1.Phone)
                 .And.HaveNoElement("Fax")
                 .And.HaveNoElement("Address")
                 .And.HaveNoElement("PostalCode")
                 .And.HaveNoElement("Region");
            }
            pid.Should().HaveCount(mFixture.Snapshot.Products.Count);
        }

        [Fact]
        public void SelectAll_Take_Skip()
        {
            var expected = mFixture.Snapshot.Products.OrderBy(p => p.ProductID).Skip(7).Take(12);

            object result = mFixture.Processor.SelectData(new Uri("/Product?$top=12&$skip=7&$orderby=ProductID", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveElementMatching("odata.nextLink", v => v is string s && s == "/Product?$orderby=ProductID&$top=12&$skip=19");

            var collection = result.As<IDictionary<string, object>>()["value"]
                .As<IReadOnlyCollection<object>>();

            collection.Should().HaveCount(12);
            collection.Select(p => p.As<IDictionary<string, object>>()["ProductID"]).Should().BeInAscendingOrder();

            foreach (IDictionary<string, object> element in collection)
            {
                Product product = null;
                element.Should()
                    .HaveElementMatching("ProductID", v =>
                    {
                        product = expected.FirstOrDefault(p => p.ProductID == (int)v);
                        return product != null;
                    });
                element.Should()
                    .HaveElement("ProductName", product.ProductName)
                    .And.HaveElement("ReorderLevel", product.ReorderLevel)
                    .And.HaveElement("Discontinued", product.Discontinued)
                    .And.HaveElement("UnitPrice", product.UnitPrice)
                    .And.HaveElement("UnitsInStock", product.UnitsInStock)
                    .And.HaveElement("UnitsOnOrder", product.UnitsOnOrder)
                    .And.HaveElement("QuantityPerUnit", product.QuantityPerUnit)
                    .And.HaveElement("categoryID", product.Category.CategoryID)
                    .And.HaveElement("supplierID", product.Supplier.SupplierID);
            }
        }

        [Fact]
        public void SelectAll_Take_Skip_Count1()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product?$top=10&$skip=5&$orderby=ProductID&$count=true", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("odata.count", v => (int)Convert.ChangeType(v, typeof(int)) == mFixture.Snapshot.Products.Count);
        }

        [Fact]
        public void SelectAll_Take_Skip_Count2()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product?$top=10&$skip=5&$orderby=ProductID&$inlinecount=allpages", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("odata.count", v => (int)Convert.ChangeType(v, typeof(int)) == mFixture.Snapshot.Products.Count);
        }

        [Fact]
        public void SelectAll_Take_Skip_Auto()
        {
            mFixture.Builder.SetEntityPagingLimitByName("Product_Type", 5);
            using var onend = new DelayedAction(() => mFixture.Builder.SetEntityPagingLimitByName("Product_Type", 0));

            object result = mFixture.Processor.SelectData(new Uri("/Product?$orderby=ProductID", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveElementMatching("odata.nextLink", v => v is string s && s == "/Product?$orderby=ProductID&$top=5&$skip=5");

            var coll = result.As<IDictionary<string, object>>()["value"].As<IReadOnlyCollection<object>>();
            var nextLink = result.As<IDictionary<string, object>>()["odata.nextLink"].As<string>();
            coll.Should().HaveCount(5);

            result = mFixture.Processor.SelectData(new Uri(nextLink, UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveElementMatching("odata.nextLink", v => v is string s && s == "/Product?$orderby=ProductID&$top=5&$skip=10");
        }

        [Fact]
        public void SelectAll_SkipToken_Ignored()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveNoElement("odata.nextLink");

            var coll = result.As<IDictionary<string, object>>()["value"].As<IReadOnlyCollection<object>>();
            coll.Should().HaveCount(mFixture.Snapshot.Products.Count);
        }

        [Fact]
        public void SelectAll_SkipToken_NoTokenSpecified()
        {
            mFixture.Builder.SetEntityPagingLimitByName("Product_Type", 5);
            using var onend = new DelayedAction(() => mFixture.Builder.SetEntityPagingLimitByName("Product_Type", 0));

            object result = mFixture.Processor.SelectData(new Uri("/Product", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveElementMatching("odata.nextLink", v => v is string s && s == "/Product?$top=5&$skip=5");

            var coll = result.As<IDictionary<string, object>>()["value"].As<IReadOnlyCollection<object>>();
            var nextLink = result.As<IDictionary<string, object>>()["odata.nextLink"].As<string>();
            coll.Should().HaveCount(5);

            result = mFixture.Processor.SelectData(new Uri(nextLink, UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveElementMatching("odata.nextLink", v => v is string s && s == "/Product?$top=5&$skip=10");
        }

        [Fact]
        public void Select_Metadata_Link()
        {
            object result = mFixture.Processor.SelectData(new Uri("/Product?$orderby=ProductID", UriKind.Relative));

            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>)
                .And.HaveElementMatching("odata.metadata", v => v is string s && s == "/$metadata#Product");
        }

        private static void Select_Metadata_Entity_ValidateProperty(XmlNode feed, string propertyName, string typeName, bool key, string valueTypeName)
        {
            string xpath = $"./a:entry[count(./a:id[text() = 'northwind.{typeName}_Type.{propertyName}']) > 0]";
            feed.Should().HaveElement(xpath);
            var entry = feed.SelectSingleNodeEx(xpath);

            entry.Should()
                .HaveElementWithValue("a:title", $"{typeName}.{propertyName}")
                .And.HaveElement("a:category")
                    .Which.Should()
                    .HaveAttribute("term", "System.Data.Services.Providers.ResourceProperty")
                    .And.HaveAttribute("scheme", "http://schemas.microsoft.com/ado/2007/08/dataservices/scheme");

            entry.Should().HaveElement("a:content")
                .Which.Should()
                    .HaveAttribute("type", "application/xml")
                    .And.HaveElement("m:properties")
                    .Which.Should()
                        .HaveElementWithValue("./d:FullName", $"northwind.{typeName}.{propertyName}")
                        .And.HaveElementWithValue("./d:IsKey", key ? "True" : "False")
                        .And.HaveElementWithValue("./d:ResourceTypeName", valueTypeName);
        }

        [Fact]
        public void Select_Metadata_Entity()
        {
            object result = mFixture.Processor.SelectData(new Uri("/$metadata#Product", UriKind.Relative));
            result.Should().BeAssignableTo<XmlDocument>();
            var doc = result.As<XmlDocument>();
            doc.Should().HaveElement("/a:entry")
                .Which.Should()
                    .HaveAttribute("xmlns:a", "http://www.w3.org/2005/Atom")
                    .And.HaveAttribute("xmlns:d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
                    .And.HaveAttribute("xmlns:m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            doc.Should().HaveElement("/a:entry/a:id")
                .Which.Should()
                    .HaveText("northwind.Product_Type");

            doc.Should().HaveElement("/a:entry/a:title")
                .Which.Should()
                    .HaveText("Product");

            doc.Should().HaveElement("/a:entry/a:link")
                .Which.Should()
                    .HaveAttribute("rel", "http://schemas.microsoft.com/ado/2007/08/dataservices/related/Properties")
                    .And.HaveAttribute("href", "/$metadata#Product/Properties")
                    .And.HaveElement("./m:inline/a:feed");

            var feed = doc.SelectSingleNodeEx("/a:entry/a:link/m:inline/a:feed");

            Select_Metadata_Entity_ValidateProperty(feed, "ProductID", "Product", true, "Edm.Int32");
            Select_Metadata_Entity_ValidateProperty(feed, "ProductName", "Product", false, "Edm.String");
            Select_Metadata_Entity_ValidateProperty(feed, "Discontinued", "Product", false, "Edm.Boolean");
            Select_Metadata_Entity_ValidateProperty(feed, "ReorderLevel", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "UnitsOnOrder", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "UnitsInStock", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "UnitPrice", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "QuantityPerUnit", "Product", false, "Edm.String");

            Select_Metadata_Entity_ValidateProperty(feed, "Category", "Product", false, "northwind.Category_Type");
            Select_Metadata_Entity_ValidateProperty(feed, "Supplier", "Product", false, "northwind.Supplier_Type");
            Select_Metadata_Entity_ValidateProperty(feed, "OrderDetail", "Product", false, "Collection(northwind.OrderDetail_Type)");
        }

        [Fact]
        public void Select_Metadata_Properties()
        {
            object result = mFixture.Processor.SelectData(new Uri("/$metadata#Product/Properties", UriKind.Relative));

            result.Should().BeAssignableTo<XmlDocument>();
            var doc = result.As<XmlDocument>();

            doc.Should().HaveElement("/a:feed")
                .Which.Should()
                    .HaveAttribute("xmlns:a", "http://www.w3.org/2005/Atom")
                    .And.HaveAttribute("xmlns:d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
                    .And.HaveAttribute("xmlns:m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata");

            var feed = doc.SelectSingleNodeEx("/a:feed");

            Select_Metadata_Entity_ValidateProperty(feed, "ProductID", "Product", true, "Edm.Int32");
            Select_Metadata_Entity_ValidateProperty(feed, "ProductName", "Product", false, "Edm.String");
            Select_Metadata_Entity_ValidateProperty(feed, "Discontinued", "Product", false, "Edm.Boolean");
            Select_Metadata_Entity_ValidateProperty(feed, "ReorderLevel", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "UnitsOnOrder", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "UnitsInStock", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "UnitPrice", "Product", false, "Edm.Double");
            Select_Metadata_Entity_ValidateProperty(feed, "QuantityPerUnit", "Product", false, "Edm.String");

            Select_Metadata_Entity_ValidateProperty(feed, "Category", "Product", false, "northwind.Category_Type");
            Select_Metadata_Entity_ValidateProperty(feed, "Supplier", "Product", false, "northwind.Supplier_Type");
            Select_Metadata_Entity_ValidateProperty(feed, "OrderDetail", "Product", false, "Collection(northwind.OrderDetail_Type)");
        }

        [Fact]
        public void SelectOne_CanDelete_No()
        {
            var category = mFixture.Snapshot.Categories[0];
            object result = mFixture.Processor.SelectData(new Uri($"/Category({category.CategoryID})?$candelete=true", UriKind.Relative));

            result.As<IDictionary<string, object>>().Should()
                            .HaveElementMatching("_candelete_", v => v is bool b && b == false);
        }

        [Fact]
        public void SelectOne_CanDelete_Yes()
        {

            var category = new Category()
            {
                CategoryName = "New Category",
                Description = ""
            };
            using (var query = mFixture.Connection.GetInsertEntityQuery<Category>())
                query.Execute(category);

            using var post = new DelayedAction(() =>
            {
                using (var query = mFixture.Connection.GetDeleteEntityQuery<Category>())
                    query.Execute(category);
            });

            object result = mFixture.Processor.SelectData(new Uri($"/Category({category.CategoryID})?$candelete=true", UriKind.Relative));

            result.As<IDictionary<string, object>>().Should()
                            .HaveElementMatching("_candelete_", v => v is bool b && b == true);
        }

        [Fact]
        public void SelectAll_CanDelete()
        {

            var category = new Category()
            {
                CategoryName = "New Category",
                Description = ""
            };
            using (var query = mFixture.Connection.GetInsertEntityQuery<Category>())
                query.Execute(category);

            using var post = new DelayedAction(() =>
            {
                using (var query = mFixture.Connection.GetDeleteEntityQuery<Category>())
                    query.Execute(category);
            });

            object result = mFixture.Processor.SelectData(new Uri($"/Category?$candelete=true", UriKind.Relative));


            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("value", v => v is IReadOnlyCollection<object>);

            var collection = result.As<IDictionary<string, object>>()["value"]
                .As<IReadOnlyCollection<object>>();

            foreach (IDictionary<string, object> element in collection)
            {
                element.Should()
                    .HaveElementMatching("CategoryID", _ => true);

                var id = (int)element["CategoryID"];
                var canDelete = id == category.CategoryID;

                element.Should()
                    .HaveElementMatching("_candelete_", v => v is bool b && b == canDelete);
            }
        }

        [Fact]
        public void SelectAll_Error()
        {
            object result = mFixture.Processor.SelectData(new Uri($"/Cutegory", UriKind.Relative));
            result.Should().BeAssignableTo<IDictionary<string, object>>();
            result.As<IDictionary<string, object>>().Should()
                .HaveElementMatching("odata.error", v => v is IDictionary<string, object>);
        }
    }
}



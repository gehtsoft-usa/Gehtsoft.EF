using System;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Xunit;
using FluentAssertions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public sealed class NorthwindFixture : IDisposable
    {
        private SqlDbConnection Connection { get; }
        private SqlCodeDomBuilder Builder { get; }

        public SqlCodeDomEnvironment CreateEnvironment() => Builder.NewEnvironment(Connection);

        public NorthwindFixture()
        {
            Connection = SqliteDbConnectionFactory.CreateMemory();

            var entities = EntityFinder.FindEntities(new[] { typeof(Northwind.Category).Assembly }, "northwind", false);
            Builder = new SqlCodeDomBuilder();
            Builder.Build(entities, "northwind");

            Snapshot northwindData = new Snapshot();
            northwindData.CreateAsync(Connection).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Connection.Dispose();
        }
    }

    public class Samples : IClassFixture<NorthwindFixture>
    {
        private readonly NorthwindFixture mNorthwind;

        private SqlCodeDomEnvironment CreateEnvironment() => mNorthwind.CreateEnvironment();

        public Samples(NorthwindFixture northwind)
        {
            mNorthwind = northwind;
        }

        [Fact]
        public void SampleSelect1()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM Category");
            var categories = statement(null);
            foreach (var category in categories)
                Console.WriteLine("{0} {1}", category.CategoryID, category.CategoryName, category.Description);
        }

        [Fact]
        public void SampleSelect2()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM Category WHERE CategoryID > 3");
            var categories = statement(null);
            foreach (var category in categories)
                Console.WriteLine("{0} {1}", category.CategoryID, category.CategoryName, category.Description);
        }

        [Fact]
        public void SampleSelect3()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM Category WHERE CategoryID > ?categoryID");
            var categories = statement(new Dictionary<string, object> { { "categoryID", 3 } });
            foreach (var category in categories)
                Console.WriteLine("{0} {1}", category.CategoryID, category.CategoryName, category.Description);

        }

        [Fact]
        public void SampleSelect4()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT * FROM OrderDetail AUTO JOIN Order AUTO JOIN Product");
            var orderDetails = statement(null);
            foreach (var orderDetail in orderDetails)
                Console.WriteLine("{0} {1} {2}", orderDetail.Order, orderDetail.Product, orderDetail.Product_ProductName);

        }

        [Fact]
        public void SampleSelect5()
        {
            var env = CreateEnvironment();
            var statement = env.Parse("query", "SELECT Order.OrderID, COUNT(*) AS DetailsCount FROM Order LEFT JOIN OrderDetail ON Order.OrderID = OrderDetail.Order GROUP BY Order.OrderID");
            var orderDetails = statement(null);
            foreach (var orderDetail in orderDetails)
                Console.WriteLine("{0} {1}", orderDetail.Order, orderDetail.Product);
        }
    }
}

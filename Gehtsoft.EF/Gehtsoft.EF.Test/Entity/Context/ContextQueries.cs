using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Context
{
    [Collection(nameof(NorthwindFixture))]
    public class ContextQueries
    {
        private readonly NorthwindFixture mFixture;

        public ContextQueries(NorthwindFixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public void Count(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<Order>();
            count.GetCount().Should().Be(mFixture.Snapshot.Orders.Count);
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public async Task CountAsync(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<Order>();
            (await count.GetCountAsync()).Should().Be(mFixture.Snapshot.Orders.Count);
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public void CountWhereReference(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<Order>();
            var customer = mFixture.Snapshot.Orders[0].Customer;
            count.Where.Property(nameof(Order.Customer)).Eq(customer);
            count.GetCount().Should().Be(mFixture.Snapshot.Orders.Count(o => o.Customer.CustomerID == customer.CustomerID)); ;
        }

        [Theory]
        [InlineData("sqlite-memory", 44.00)]
        public void CountWhereLs(string driver, double value)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<OrderDetail>();
            count.Where.Property(nameof(OrderDetail.Quantity)).Ls(value);
            count.GetCount().Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Quantity < value));
        }

        [Theory]
        [InlineData("sqlite-memory", 44.00)]
        public void CountWhereLe(string driver, double value)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<OrderDetail>();
            count.Where.Property(nameof(OrderDetail.Quantity)).Le(value);
            count.GetCount().Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Quantity < value));
        }

        [Theory]
        [InlineData("sqlite-memory", 44.00)]
        public void CountWhereGt(string driver, double value)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<OrderDetail>();
            count.Where.Property(nameof(OrderDetail.Quantity)).Gt(value);
            count.GetCount().Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Quantity > value));
        }

        [Theory]
        [InlineData("sqlite-memory", 44.00)]
        public void CountWhereGe(string driver, double value)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<OrderDetail>();
            count.Where.Property(nameof(OrderDetail.Quantity)).Ge(value);
            count.GetCount().Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Quantity >= value));
        }
        [Theory]
        [InlineData("sqlite-memory", 44.00)]
        public void CountWhereEq(string driver, double value)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<OrderDetail>();
            count.Where.Property(nameof(OrderDetail.Quantity)).Eq(value);
            count.GetCount().Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Quantity == value));
        }

        [Theory]
        [InlineData("sqlite-memory", 44.00)]
        public void CountWhereNeq(string driver, double value)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<OrderDetail>();
            count.Where.Property(nameof(OrderDetail.Quantity)).Neq(value);
            count.GetCount().Should().Be(mFixture.Snapshot.OrderDetails.Count(o => o.Quantity != value));
        }

        [Theory]
        [InlineData("sqlite-memory", "D%", 2)]
        public void CountWhereLike(string driver, string value, int expectedCount)
        {
            var connection = mFixture.GetInstance(driver);
            using var count = connection.Count<Employee>();
            count.Where.Property(nameof(Employee.LastName)).Like(value);
            count.GetCount().Should().Be(expectedCount);
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public void AddChangeDelete_OneEntity(string driver)
        {
            Category cat = null;
            var connection = mFixture.GetInstance(driver);

            using var postpone = new DelayedAction(() =>
            {
                if (cat != null && cat.CategoryID > 0)
                {
                    using var q = connection.GetDeleteEntityQuery<Category>();
                    q.Execute(cat);
                }
            });

            cat = new Category()
            {
                CategoryName = "New Category",
                Description = "Category Description"
            };

            using (var insert = connection.InsertEntity<Category>(true))
                insert.Execute(cat);

            cat.CategoryID.Should().BeGreaterThan(0);

            using (var select = connection.Select<Category>())
            {
                select.Where.Property(nameof(Category.CategoryID)).Eq(cat.CategoryID);
                var cat1 = select.ReadOne<Category>();
                cat1.Should().NotBeNull();
                cat1.CategoryID.Should().Be(cat.CategoryID);
                cat1.CategoryName.Should().Be(cat.CategoryName);
                cat1.Description.Should().Be(cat.Description);
            }

            cat.CategoryName = "New Category Name";
            cat.Description = "New Category Description";

            using (var update = connection.UpdateEntity<Category>())
                update.Execute(cat);

            using (var select = connection.Select<Category>())
            {
                select.Where.Property(nameof(Category.CategoryID)).Eq(cat.CategoryID);
                var cat1 = select.ReadOne<Category>();
                cat1.Should().NotBeNull();
                cat1.CategoryID.Should().Be(cat.CategoryID);
                cat1.CategoryName.Should().Be(cat.CategoryName);
                cat1.Description.Should().Be(cat.Description);
            }

            using (var delete = connection.DeleteEntity<Category>())
                delete.Execute(cat);

            using (var count = connection.Count<Category>())
            {
                count.Where.Property(nameof(Category.CategoryID)).Eq(cat.CategoryID);
                count.GetCount().Should().Be(0);
            }

            using (var count = connection.Count<Category>())
            {
                count.GetCount().Should().Be(mFixture.Snapshot.Categories.Count);
            }
        }

        [Theory]
        [InlineData("sqlite-memory", 1000, 10)]
        public void DeleteMultiple(string driver, int firstID, int testCount)
        {
            List<Category> cats = new List<Category>();
            var connection = mFixture.GetInstance(driver);

            using var postpone = new DelayedAction(() =>
            {
                using var q = connection.GetDeleteEntityQuery<Category>();
                foreach (var cat in cats)
                    q.Execute(cat);
            });

            using (var query = connection.InsertEntity<Category>(false))
            {
                for (int i = 0; i < testCount; i++)
                {
                    Category cat = new Category()
                    {
                        CategoryID = firstID + i,
                        CategoryName = "Name",
                        Description = "Description"
                    };
                    query.Execute(cat);
                    cats.Add(cat);
                }
            }

            using (var count = connection.Count<Category>())
            {
                count.Where.Property(nameof(Category.CategoryID)).Ge(firstID);
                count.GetCount().Should().Be(testCount);
            }

            using (var delete = connection.DeleteMultiple<Category>())
            {
                delete.Where.Property(nameof(Category.CategoryID)).Ge(firstID);
                delete.Execute();
            }

            using (var count = connection.Count<Category>())
            {
                count.Where.Property(nameof(Category.CategoryID)).Ge(firstID);
                count.GetCount().Should().Be(0);
            }

            cats.Clear();
        }
        
        [Theory]
        [InlineData("sqlite-memory")]
        public void SelectEntities_All(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            using (var select = connection.Select<Category>())
            {
                var cats = select.ReadAll<Category>();
                cats.Should().HaveCount(mFixture.Snapshot.Categories.Count);
                foreach (var cat in mFixture.Snapshot.Categories)
                    cats.Should().Contain(c =>
                            c.CategoryID == cat.CategoryID &&
                            c.CategoryName == cat.CategoryName && 
                            c.Description == cat.Description);

            }
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public async Task SelectEntities_AllAsync(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            using (var select = connection.Select<Category>())
            {
                var cats = await select.ReadAllAsync<Category>();
                cats.Should().HaveCount(mFixture.Snapshot.Categories.Count);
                foreach (var cat in mFixture.Snapshot.Categories)
                    cats.Should().Contain(c =>
                            c.CategoryID == cat.CategoryID &&
                            c.CategoryName == cat.CategoryName &&
                            c.Description == cat.Description);
            }
        }

        [Theory]
        [InlineData("sqlite-memory", "C", 2)]
        public void SelectEntities_Where(string driver, string start, int count)
        {
            var connection = mFixture.GetInstance(driver);
            using (var select = connection.Select<Category>())
            {
                select.Where.Property(nameof(Category.CategoryName)).Like(start + "%");
                var cats = select.ReadAll<Category>();
                cats.Should().HaveCount(count);
                cats.All(c => c.CategoryName.StartsWith(start)).Should().BeTrue();
            }
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public void SelectEntities_Get(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            var id = mFixture.Snapshot.Orders[0].OrderID;
            var order = connection.Get<Order>(id);
            order.Should().NotBeNull();
            order.OrderID.Should().Be(id);
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public async Task SelectEntities_GetAsync(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            var id = mFixture.Snapshot.Orders[0].OrderID;
            var order = await connection.GetAsync<Order>(id);
            order.Should().NotBeNull();
            order.OrderID.Should().Be(id);
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public void SelectEntities_Exist(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            var id = mFixture.Snapshot.Orders[0].OrderID;
            connection.Exists<Order>(id).Should().BeTrue();

            connection.Exists<Order>(1000000).Should().BeFalse();
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public async Task SelectEntities_ExistAsync(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            var id = mFixture.Snapshot.Orders[0].OrderID;
            (await connection.ExistsAsync<Order>(id)).Should().BeTrue();
        }

        [Theory]
        [InlineData("sqlite-memory")]
        public void SelectEntities_Save(string driver)
        {
            var connection = mFixture.GetInstance(driver);
            
            var cat = new Category()
            {
                CategoryID = 10000,
                CategoryName = "New Category",
                Description = "Description"
            };

            using var postpone = new DelayedAction(() =>
            {
                using var q = connection.DeleteMultiple<Category>();
                q.Where.Property(nameof(Category.CategoryID)).Ge(10000);
                q.Execute();
            });

            connection.Save(cat, false);
            cat.CategoryID.Should().Be(10000);

            using (var count = connection.Count<Category>())
                count.GetCount().Should().Be(mFixture.Snapshot.Categories.Count + 1);

            var cat1 = connection.Get<Category>(cat.CategoryID);
            cat1.Should().NotBeNull();
            cat1.CategoryName.Should().Be("New Category");
            cat1.Description.Should().Be("Description");

            cat.CategoryName += "1";
            cat.Description += "2";

            connection.Save(cat);

            using (var count = connection.Count<Category>())
                count.GetCount().Should().Be(mFixture.Snapshot.Categories.Count + 1);

            cat1 = connection.Get<Category>(cat.CategoryID);
            cat1.Should().NotBeNull();
            cat1.CategoryName.Should().Be("New Category1");
            cat1.Description.Should().Be("Description2");
        }
    }
}

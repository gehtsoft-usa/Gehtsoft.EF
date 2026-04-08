using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Bson;
using Xunit;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Gehtsoft.EF.Test.Legacy.Mongo
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class MongoNoRefTests : IClassFixture<MongoNoRefTests.Fixture>
    {
        [Entity(Table = "datatest1", Scope = "mongo0")]
        public class DataTestEntity
        {
            [EntityProperty(AutoId = true)]
            public object ID { get; set; }

            [EntityProperty]
            public string StringVal { get; set; }

            [EntityProperty(Sorted = true, Field = "intval")]
            public int IntVal { get; set; }

            [EntityProperty]
            public DateTime? DateVal { get; set; }

            [EntityProperty]
            public bool BoolVal { get; set; }

            [EntityProperty]
            public Guid?[] GuidValArr { get; set; }
        }

        [Entity(Table = "category1", Scope = "mongo1")]
        public class Category
        {
            [EntityProperty(AutoId = true)]
            public object ID { get; set; }

            [EntityProperty(Size = 64)]
            public string Name { get; set; }
        }

        [Entity(Table = "good1", Scope = "mongo1")]
        public class Good
        {
            [EntityProperty(AutoId = true)]
            public object ID { get; set; }

            [EntityProperty(Size = 64)]
            public string Name { get; set; }

            [EntityProperty]
            public Category Category { get; set; }
        }

        [MongoIndex(nameof(Transaction.Goods) + "." + nameof(Good.ID))]
        [MongoIndex(nameof(Transaction.Goods) + "." + nameof(Good.Category) + "." + nameof(Category.ID))]
        [Entity(Table = "transaction1", Scope = "mongo1")]
        public class Transaction
        {
            [EntityProperty(AutoId = true)]
            public object ID { get; set; }

            [EntityProperty]
            public DateTime Timestamp { get; set; }

            [EntityProperty]
            public double Total { get; set; }

            [EntityProperty]
            public int Index { get; set; }

            [EntityProperty(Sorted = true)]
            public Good[] Goods { get; set; }
        }

        [Entity(Scope = "mongo2")]
        public class TestNoIdEntity
        {
            [EntityProperty(Sorted = true)]
            public string ID { get; set; }

            [EntityProperty]
            public string StringVal { get; set; }
        }

        [Entity]
        public class TestContextEntity
        {
            [AutoId]
            public object ID { get; set; }

            [EntityProperty(Sorted = true, Size = 64)]
            public string Name { get; set; }

            [EntityProperty(Sorted = true, Size = 12, Precision = 2)]
            public double Value { get; set; }
        }

        public class Fixture : MongoConnectionFixtureBase
        {
            protected override void ConfigureConnection(MongoConnection connection)
            {
                base.ConfigureConnection(connection);
            }
        }

        private readonly Fixture mFixture;

        public MongoNoRefTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames() => SqlConnectionSources.MongoConnectionNames();

        [TestOrder(1)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void PlainQuery(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            EntityFinder.EntityTypeInfo[] infos = EntityFinder.FindEntities(new Assembly[] { typeof(MongoNoRefTests).GetTypeInfo().Assembly }, "mongo0", false);
            EntityFinder.ArrageEntities(infos);

            foreach (EntityFinder.EntityTypeInfo info in infos.Reverse())
                connection.GetDeleteListQuery(info.EntityType).Execute();

            connection.GetSchema().Count(s => s == "datatest1").Should().Be(0);

            foreach (EntityFinder.EntityTypeInfo info in infos)
                connection.GetCreateListQuery(info.EntityType).Execute();

            connection.GetSchema().Count(s => s == "datatest1").Should().Be(1);

            DataTestEntity e1, e2, e3;

            using (var query = connection.GetInsertEntityQuery<DataTestEntity>())
            {
                e1 = new DataTestEntity()
                {
                    IntVal = 1,
                    StringVal = "s1",
                    BoolVal = true,
                    DateVal = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Local),
                    GuidValArr = new Guid?[] { Guid.NewGuid(), null, Guid.NewGuid() },
                };

                query.Execute(e1);

                e2 = new DataTestEntity()
                {
                    IntVal = 2,
                    StringVal = null,
                    BoolVal = false,
                    DateVal = new DateTime(2004, 5, 6, 7, 0, 12, DateTimeKind.Local),
                    GuidValArr = null,
                };

                query.Execute(e2);

                e3 = new DataTestEntity()
                {
                    IntVal = 3,
                    StringVal = "s3",
                    BoolVal = true,
                    DateVal = null,
                    GuidValArr = null,
                };

                query.Execute(e3);
            }

            using (MongoSelectQuery query = connection.GetSelectQuery<DataTestEntity>())
            {
                query.AddOrderBy(nameof(DataTestEntity.IntVal), SortDir.Asc);
                query.Execute();

                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(2).Should().Be(1);
                query.IsNull(1).Should().BeFalse();
                query.IsNull("stringval").Should().BeFalse();
                query.GetValue<string>(1).Should().Be("s1");
                query.GetValue<DateTime>("dateval").Should().Be(e1.DateVal.Value);

                Guid?[] arr = query.GetValue<Guid?[]>("guidvalarr");
                arr.Should().NotBeNull();
                arr.Length.Should().Be(e1.GuidValArr.Length);
                arr[0].Should().Be(e1.GuidValArr[0]);
                e1.GuidValArr[1].Should().BeNull();
                arr[2].Should().Be(e1.GuidValArr[2]);

                query.ReadNext().Should().BeTrue();
                query.GetValue<int>("intval").Should().Be(2);
                query.IsNull(1).Should().BeTrue();
                query.GetValue<string>(1).Should().BeNull();
                query.GetValue<object>(1).Should().BeNull();
                query.IsNull("GuidValArr").Should().BeTrue();
                query.GetValue<DateTime?>("dateval").Should().Be(e2.DateVal);

                query.ReadNext().Should().BeTrue();
                query.GetValue<int>(2).Should().Be(3);
                query.GetValue<DateTime?>("dateval").Should().Be(e3.DateVal);
                query.ReadNext().Should().BeFalse();
            }

            using (MongoSelectQuery query = connection.GetSelectQuery<DataTestEntity>())
            {
                query.AddToResultset(nameof(DataTestEntity.IntVal));
                query.AddToResultset(nameof(DataTestEntity.StringVal));
                query.ExcludeFromResultset(nameof(DataTestEntity.ID));
                query.AddOrderBy(nameof(DataTestEntity.IntVal), SortDir.Asc);
                query.Execute();
                query.ReadNext();
                query.FieldCount.Should().Be(2);
                query.FieldName(1).Should().Be("intval");
                query.FieldName(0).Should().Be("stringval");
            }

            using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
                query.RowCount.Should().Be(3);

            using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
                query.RowCount.Should().Be(3);

            using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
            {
                query.Where.Property(nameof(DataTestEntity.IntVal)).Eq(2);
                query.RowCount.Should().Be(1);
            }

            using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
            {
                query.Where.Property(nameof(DataTestEntity.IntVal)).Eq(200);
                query.RowCount.Should().Be(0);
            }
        }

        [TestOrder(2)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void EntityQuery(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            //create lists
            EntityFinder.EntityTypeInfo[] infos = EntityFinder.FindEntities(new Assembly[] { typeof(MongoNoRefTests).GetTypeInfo().Assembly }, "mongo1", false);
            EntityFinder.ArrageEntities(infos);

            foreach (EntityFinder.EntityTypeInfo info in infos.Reverse())
                connection.GetDeleteListQuery(info.EntityType).Execute();

            foreach (EntityFinder.EntityTypeInfo info in infos)
                connection.GetCreateListQuery(info.EntityType).Execute();

            //create categories
            Category catFood = new Category() { Name = "food" };
            Category catDress = new Category() { Name = "dress" };

            using (var query = connection.GetInsertEntityQuery<Category>())
            {
                query.Execute(catFood);
                catFood.ID.Should().NotBeNull();
                query.Execute(catDress);
                catDress.ID.Should().NotBeNull();
                ((ObjectId)catFood.ID).Should().NotBe((ObjectId)catDress.ID);
            }

            //create goods
            Good[] goods = new Good[10];
            using (var query = connection.GetInsertEntityQuery<Good>())
            {
                for (int i = 0; i < 5; i++)
                {
                    goods[i] = new Good() { Name = $"food {i + 1}", Category = catFood };
                    query.Execute(goods[i]);
                }

                for (int i = 5; i < goods.Length; i++)
                {
                    goods[i] = new Good() { Name = $"dress {i + 1}", Category = catDress };
                    query.Execute(goods[i]);
                }
            }

            //check query with where to the subobject
            using (var query = connection.GetSelectQuery<Good>(false))
            {
                query.Where.Property($"{nameof(Good.Category)}.{nameof(Category.ID)}").Eq(catDress.ID);
                EntityCollection<Good> coll = query.ReadAll<EntityCollection<Good>, Good>();

                coll.Count.Should().Be(goods.Length - 5);
                bool[] found = new bool[goods.Length];
                //make sure that found right
                foreach (Good good in coll)
                {
                    good.Category.Should().NotBeNull();
                    ((ObjectId)good.Category.ID).Should().Be((ObjectId)catDress.ID);
                    for (int i = 0; i < goods.Length; i++)
                    {
                        if ((ObjectId)goods[i].ID == (ObjectId)good.ID)
                        {
                            found[i] = true;
                            break;
                        }
                    }
                }

                //make sure that everything is found
                for (int i = 0; i < goods.Length; i++)
                {
                    if ((ObjectId)goods[i].Category.ID == (ObjectId)catDress.ID)
                        found[i].Should().BeTrue();
                }
            }

            //create transactions
            Transaction[] transactions = new Transaction[32];
            Random r = new Random((int)(DateTime.Now.Ticks & 32767));
            DateTime baseDate = DateTime.Today;

            using (var query = connection.GetInsertEntityQuery<Transaction>())
            {
                for (int i = 0; i < transactions.Length; i++)
                {
                    Transaction transaction = new Transaction()
                    {
                        Goods = new Good[r.Next(1, 4)],
                        Total = r.Next(1000, 5000),
                        Timestamp = baseDate.AddDays(r.Next(-10, 10)),
                        Index = i,
                    };

                    for (int j = 0; j < transaction.Goods.Length; j++)
                    {
                        transaction.Goods[j] = goods[r.Next(goods.Length)];
                    }

                    transactions[i] = transaction;
                    query.Execute(transaction);
                }
            }

            using (var query = connection.GetSelectQuery<Transaction>())
            {
                query.AddOrderBy(nameof(Transaction.Timestamp), SortDir.Asc);
                EntityCollection<Transaction> coll = query.ReadAll<EntityCollection<Transaction>, Transaction>();

                coll.Count.Should().Be(transactions.Length);

                DateTime prev = new DateTime(0, DateTimeKind.Unspecified);

                foreach (Transaction tr in coll)
                {
                    prev.Ticks.Should().BeLessThanOrEqualTo(tr.Timestamp.Ticks);
                    prev = tr.Timestamp;

                    Transaction otr = transactions[tr.Index];
                    tr.Timestamp.Should().Be(otr.Timestamp);
                    tr.Total.Should().Be(otr.Total);
                    tr.Goods.Length.Should().Be(otr.Goods.Length);

                    for (int i = 0; i < tr.Goods.Length; i++)
                    {
                        Good good = tr.Goods[i], ogood = otr.Goods[i];
                        ((ObjectId)good.ID).Should().Be((ObjectId)ogood.ID);
                        good.Name.Should().Be(ogood.Name);
                        ((ObjectId)good.Category.ID).Should().Be((ObjectId)ogood.Category.ID);
                        good.Category.Name.Should().Be(ogood.Category.Name);
                    }

                    tr.Total = r.Next(2000, 5000);
                    using (var uquery = connection.GetUpdateEntityQuery<Transaction>())
                        uquery.Execute(tr);
                }
            }

            bool checkPoint1 = false, checkPoint2 = false;

            for (int i = 0; i < transactions.Length; i++)
            {
                if (transactions[i].Goods.Length > 1)
                {
                    checkPoint1 = true;
                    Good good = transactions[i].Goods[1];

                    //test for any array item query
                    using (var query = connection.GetSelectQuery<Transaction>())
                    {
                        query.Where.Property($"{nameof(Transaction.Goods)}.{nameof(Good.ID)}").Eq(good.ID);
                        EntityCollection<Transaction> coll = query.ReadAll<Transaction>();
                        foreach (Transaction tr in coll)
                        {
                            bool found = false;
                            for (int j = 0; j < tr.Goods.Length && !found; j++)
                            {
                                found |= (ObjectId)tr.Goods[j].ID == (ObjectId)good.ID;
                                if (found && j != 1)
                                    checkPoint2 = true;
                            }

                            found.Should().BeTrue();
                        }
                    }

                    //test for specific array item query
                    using (var query = connection.GetSelectQuery<Transaction>())
                    {
                        query.Where.Property($"{nameof(Transaction.Goods)}.1.{nameof(Good.ID)}").Eq(good.ID);
                        EntityCollection<Transaction> coll = query.ReadAll<Transaction>();
                        foreach (Transaction tr in coll)
                        {
                            tr.Goods.Length.Should().BeGreaterThanOrEqualTo(2);
                            ((ObjectId)tr.Goods[1].ID).Should().Be((ObjectId)good.ID);
                        }
                    }
                }
            }

            checkPoint1.Should().BeTrue();
            checkPoint2.Should().BeTrue();
        }

        [TestOrder(3)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void EntityQueryNoId(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            EntityFinder.EntityTypeInfo[] infos = EntityFinder.FindEntities(new Assembly[] { typeof(MongoNoRefTests).GetTypeInfo().Assembly }, "mongo2", false);
            EntityFinder.ArrageEntities(infos);

            foreach (EntityFinder.EntityTypeInfo info in infos.Reverse())
                connection.GetDeleteListQuery(info.EntityType).Execute();

            foreach (EntityFinder.EntityTypeInfo info in infos)
                connection.GetCreateListQuery(info.EntityType).Execute();

            using (var query = connection.GetCountQuery<TestNoIdEntity>())
                query.RowCount.Should().Be(0);

            TestNoIdEntity entity = new TestNoIdEntity()
            {
                ID = "a",
                StringVal = "b"
            };

            using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
            {
                query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("a");
                query.Execute(entity);
            }

            using (var query = connection.GetCountQuery<TestNoIdEntity>())
                query.RowCount.Should().Be(0);

            using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
            {
                query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("a");
                query.InsertIfNotExists = true;
                query.Execute(entity);
            }

            using (var query = connection.GetCountQuery<TestNoIdEntity>())
                query.RowCount.Should().Be(1);

            using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
            {
                query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("a");
                query.InsertIfNotExists = true;
                query.Execute(entity);
            }

            using (var query = connection.GetCountQuery<TestNoIdEntity>())
                query.RowCount.Should().Be(1);

            using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
            {
                query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("b");
                query.InsertIfNotExists = true;
                query.Execute(entity);
            }

            using (var query = connection.GetCountQuery<TestNoIdEntity>())
                query.RowCount.Should().Be(2);
        }

        [TestOrder(4)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void EntityContext(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            try
            {
                using (var query = connection.DropEntity<TestContextEntity>())
                    query.Execute();
                using (var query = connection.CreateEntity<TestContextEntity>())
                    query.Execute();
                using (var transaction = ((IEntityContext)connection).BeginTransaction())
                {
                    using (var query = connection.InsertEntity<TestContextEntity>())
                    {
                        Random r = new Random();
                        for (int i = 0; i < 100; i++)
                        {
                            var e = new TestContextEntity()
                            {
                                Name = "Name " + (i + 1),
                            };
                            if (i == 0)
                                e.Value = 0;
                            else if (i == 1)
                                e.Value = 100;
                            else
                                e.Value = r.NextDouble() * 100;
                            query.Execute(e);
                        }
                    }
                    transaction.Commit();
                }

                using (var query = connection.Count<TestContextEntity>())
                    query.GetCount().Should().Be(100);

                EntityCollection<TestContextEntity> collection1, collection2;

                using (var query = connection.Select<TestContextEntity>())
                {
                    query.Order.Add(nameof(TestContextEntity.Value));
                    query.Execute();
                    collection1 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection1.Count.Should().Be(100);
                collection1.Should().BeInAscendingOrder(v => v.Value);

                using (var query = connection.Select<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Ls(50);
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().BeLessThan(collection1.Count);
                collection2.Count.Should().BeGreaterThan(0);
                collection2.Should().OnlyContain(e => e.Value < 50);

                using (var query = connection.Select<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Gt(50);
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().BeLessThan(collection1.Count);
                collection2.Count.Should().BeGreaterThan(0);
                collection2.Should().OnlyContain(e => e.Value > 50);

                using (var query = connection.Select<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Name)).Like("Name 1%");
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().BeLessThan(collection1.Count);
                collection2.Count.Should().BeGreaterThan(0);
                collection2.Should().OnlyContain(e => e.Name.StartsWith("Name 1"));

                using (var query = connection.Select<TestContextEntity>())
                {
                    query.Take = 10;
                    query.Skip = 2;
                    query.Order.Add(nameof(TestContextEntity.Value));
                    query.Execute();
                    collection2 = query.ReadAll<EntityCollection<TestContextEntity>, TestContextEntity>();
                }

                collection2.Count.Should().Be(10);
                for (int i = 0; i < 10; i++)
                    collection2[i].ID.Should().Be(collection1[i + 2].ID);

                var entity = connection.Get<TestContextEntity>(collection2[2].ID);
                entity.Should().NotBeNull();
                entity.ID.Should().Be(collection2[2].ID);

                entity.Name = "New Name";
                connection.Save<TestContextEntity>(entity);
                entity = connection.Get<TestContextEntity>(collection2[2].ID);
                entity.ID.Should().Be(collection2[2].ID);
                entity.Name.Should().Be("New Name");

                using (var query = connection.DeleteEntity<TestContextEntity>())
                    query.Execute(entity);

                entity = connection.Get<TestContextEntity>(collection2[2].ID);
                entity.Should().BeNull();

                using (var query = connection.Count<TestContextEntity>())
                    query.GetCount().Should().Be(99);

                entity = new TestContextEntity()
                {
                    Name = "New Entity",
                    Value = 500,
                };
                connection.Save(entity);
                entity.ID.Should().NotBeNull();

                var entity1 = connection.Get<TestContextEntity>(entity.ID);
                entity1.Should().NotBeNull();
                entity1.ID.Should().Be(entity.ID);
                entity1.Name.Should().Be(entity.Name);
                entity1.Value.Should().Be(entity.Value);

                using (var query = connection.DeleteMultiple<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Ls(20);
                    query.Execute();
                }

                using (var query = connection.Count<TestContextEntity>())
                {
                    query.Where.Property(nameof(TestContextEntity.Value)).Ls(20);
                    query.Execute();
                    query.GetCount().Should().Be(0);
                }
            }
            finally
            {
                using (var query = connection.DropEntity<TestContextEntity>())
                    query.Execute();
            }
        }
    }
}

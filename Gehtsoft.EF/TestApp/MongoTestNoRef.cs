using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Utils;
using Gehtsoft.Tools.TypeUtils;
using MongoDB.Bson;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace TestApp
{
    [TestFixture]
    public class MongoTestNoRef
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

        [Test]
        public void TestPlainQuery()
        {
            using (MongoConnection connection = MongoConnectionFactory.Create(Config.Instance.Mongo))
            {
                EntityFinder.EntityTypeInfo[] infos = EntityFinder.FindEntities(new Assembly[] { typeof(MongoTestNoRef).GetTypeInfo().Assembly }, "mongo0", false);
                EntityFinder.ArrageEntities(infos);

                foreach (EntityFinder.EntityTypeInfo info in infos.Reverse())
                    connection.GetDeleteListQuery(info.EntityType).Execute();

                ClassicAssert.AreEqual(0, connection.GetSchema().Count(s => s == "datatest1"));

                foreach (EntityFinder.EntityTypeInfo info in infos)
                    connection.GetCreateListQuery(info.EntityType).Execute();

                ClassicAssert.AreEqual(1, connection.GetSchema().Count(s => s == "datatest1"));

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

                    ClassicAssert.IsTrue(query.ReadNext());
                    ClassicAssert.AreEqual(1, query.GetValue<int>(2));
                    ClassicAssert.IsFalse(query.IsNull(1));
                    ClassicAssert.IsFalse(query.IsNull("stringval"));
                    ClassicAssert.AreEqual("s1", query.GetValue<string>(1));
                    ClassicAssert.AreEqual(e1.DateVal, query.GetValue<DateTime>("dateval"));

                    Guid?[] arr = query.GetValue<Guid?[]>("guidvalarr");
                    ClassicAssert.IsNotNull(arr);
                    ClassicAssert.AreEqual(e1.GuidValArr.Length, arr.Length);
                    ClassicAssert.AreEqual(e1.GuidValArr[0], arr[0]);
                    ClassicAssert.IsNull(e1.GuidValArr[1]);
                    ClassicAssert.AreEqual(e1.GuidValArr[2], arr[2]);

                    ClassicAssert.IsTrue(query.ReadNext());
                    ClassicAssert.AreEqual(2, query.GetValue<int>("intval"));
                    ClassicAssert.IsTrue(query.IsNull(1));
                    ClassicAssert.AreEqual(null, query.GetValue<string>(1));
                    ClassicAssert.AreEqual(null, query.GetValue<object>(1));
                    ClassicAssert.IsTrue(query.IsNull("GuidValArr"));
                    ClassicAssert.AreEqual(e2.DateVal, query.GetValue<DateTime?>("dateval"));

                    ClassicAssert.IsTrue(query.ReadNext());
                    ClassicAssert.AreEqual(3, query.GetValue<int>(2));
                    ClassicAssert.AreEqual(e3.DateVal, query.GetValue<DateTime?>("dateval"));
                    ClassicAssert.IsFalse(query.ReadNext());
                }

                using (MongoSelectQuery query = connection.GetSelectQuery<DataTestEntity>())
                {
                    query.AddToResultset(nameof(DataTestEntity.IntVal));
                    query.AddToResultset(nameof(DataTestEntity.StringVal));
                    query.ExcludeFromResultset(nameof(DataTestEntity.ID));
                    query.AddOrderBy(nameof(DataTestEntity.IntVal), SortDir.Asc);
                    query.Execute();
                    query.ReadNext();
                    ClassicAssert.AreEqual(2, query.FieldCount);
                    ClassicAssert.AreEqual("intval", query.FieldName(1));
                    ClassicAssert.AreEqual("stringval", query.FieldName(0));
                }

                using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
                    ClassicAssert.AreEqual(3, query.RowCount);

                using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
                    ClassicAssert.AreEqual(3, query.RowCount);

                using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
                {
                    query.Where.Property(nameof(DataTestEntity.IntVal)).Eq(2);
                    ClassicAssert.AreEqual(1, query.RowCount);
                }

                using (MongoCountQuery query = connection.GetCountQuery<DataTestEntity>())
                {
                    query.Where.Property(nameof(DataTestEntity.IntVal)).Eq(200);
                    ClassicAssert.AreEqual(0, query.RowCount);
                }
            }
        }

        [Test]
        public void TestEntityQuery()
        {
            using (MongoConnection connection = MongoConnectionFactory.Create(Config.Instance.Mongo))
            {
                //create lists

                EntityFinder.EntityTypeInfo[] infos = EntityFinder.FindEntities(new Assembly[] { typeof(MongoTestNoRef).GetTypeInfo().Assembly }, "mongo1", false);
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
                    ClassicAssert.IsNotNull(catFood.ID);
                    query.Execute(catDress);
                    ClassicAssert.IsNotNull(catDress.ID);
                    ClassicAssert.AreNotEqual((ObjectId)catFood.ID, (ObjectId)catDress.ID);
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

                    ClassicAssert.AreEqual(goods.Length - 5, coll.Count);
                    bool[] found = new bool[goods.Length];
                    //make sure that found right
                    foreach (Good good in coll)
                    {
                        ClassicAssert.IsNotNull(good.Category);
                        ClassicAssert.AreEqual((ObjectId)catDress.ID, (ObjectId)good.Category.ID);
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
                            ClassicAssert.IsTrue(found[i]);
                    }
                }

                //create transactions
                Transaction[] transactions = new Transaction[32];
                Random r = new Random((int)(DateTime.Now.Ticks & 32767));
                DateTime baseDate = DateTime.Now.TruncateTime();

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

                    ClassicAssert.AreEqual(transactions.Length, coll.Count);

                    DateTime prev = new DateTime(0, DateTimeKind.Unspecified);

                    foreach (Transaction tr in coll)
                    {
                        ClassicAssert.IsTrue(prev.Ticks <= tr.Timestamp.Ticks);
                        prev = tr.Timestamp;

                        Transaction otr = transactions[tr.Index];
                        ClassicAssert.AreEqual(otr.Timestamp, tr.Timestamp);
                        ClassicAssert.AreEqual(otr.Total, tr.Total);
                        ClassicAssert.AreEqual(otr.Goods.Length, tr.Goods.Length);

                        for (int i = 0; i < tr.Goods.Length; i++)
                        {
                            Good good = tr.Goods[i], ogood = otr.Goods[i];
                            ClassicAssert.AreEqual((ObjectId)ogood.ID, (ObjectId)good.ID);
                            ClassicAssert.AreEqual(ogood.Name, good.Name);
                            ClassicAssert.AreEqual((ObjectId)ogood.Category.ID, (ObjectId)good.Category.ID);
                            ClassicAssert.AreEqual(ogood.Category.Name, good.Category.Name);
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

                                ClassicAssert.IsTrue(found);
                            }
                        }

                        //test for specific array item query
                        using (var query = connection.GetSelectQuery<Transaction>())
                        {
                            query.Where.Property($"{nameof(Transaction.Goods)}.1.{nameof(Good.ID)}").Eq(good.ID);
                            EntityCollection<Transaction> coll = query.ReadAll<Transaction>();
                            foreach (Transaction tr in coll)
                            {
                                ClassicAssert.IsTrue(tr.Goods.Length >= 2);
                                ClassicAssert.AreEqual((ObjectId)tr.Goods[1].ID, (ObjectId)good.ID);
                            }
                        }
                    }
                }

                ClassicAssert.IsTrue(checkPoint1);
                ClassicAssert.IsTrue(checkPoint2);
            }
        }

        [Entity(Scope = "mongo2")]
        public class TestNoIdEntity
        {
            [EntityProperty(Sorted = true)]
            public string ID { get; set; }

            [EntityProperty]
            public string StringVal { get; set; }
        }

        [Test]
        public void TestEntityQuery1()
        {
            using (MongoConnection connection = MongoConnectionFactory.Create(Config.Instance.Mongo))
            {
                EntityFinder.EntityTypeInfo[] infos = EntityFinder.FindEntities(new Assembly[] { typeof(MongoTestNoRef).GetTypeInfo().Assembly }, "mongo2", false);
                EntityFinder.ArrageEntities(infos);

                foreach (EntityFinder.EntityTypeInfo info in infos.Reverse())
                    connection.GetDeleteListQuery(info.EntityType).Execute();

                foreach (EntityFinder.EntityTypeInfo info in infos)
                    connection.GetCreateListQuery(info.EntityType).Execute();

                using (var query = connection.GetCountQuery<TestNoIdEntity>())
                    ClassicAssert.AreEqual(0, query.RowCount);

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
                    ClassicAssert.AreEqual(0, query.RowCount);

                using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
                {
                    query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("a");
                    query.InsertIfNotExists = true;
                    query.Execute(entity);
                }

                using (var query = connection.GetCountQuery<TestNoIdEntity>())
                    ClassicAssert.AreEqual(1, query.RowCount);

                using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
                {
                    query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("a");
                    query.InsertIfNotExists = true;
                    query.Execute(entity);
                }

                using (var query = connection.GetCountQuery<TestNoIdEntity>())
                    ClassicAssert.AreEqual(1, query.RowCount);

                using (var query = connection.GetUpdateEntityQuery<TestNoIdEntity>())
                {
                    query.Where.Property(nameof(TestNoIdEntity.ID)).Eq("b");
                    query.InsertIfNotExists = true;
                    query.Execute(entity);
                }

                using (var query = connection.GetCountQuery<TestNoIdEntity>())
                    ClassicAssert.AreEqual(2, query.RowCount);
            }
        }

        [Test]
        public void TestEntityContext()
        {
            using (MongoConnection connection = MongoConnectionFactory.Create(Config.Instance.Mongo))
                EntityContextTest.Test(connection).Should().BeTrue();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Bson;
using Xunit;

namespace Gehtsoft.EF.Test.Mongo
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class MongoQueries : IClassFixture<MongoQueries.Fixture>
    {
        [Entity(Scope = "MongoQueries", NamingPolicy = EntityNamingPolicy.LowerCase, Table = "queriestest1")]
        public class EntityA
        {
            [AutoId]
            public ObjectId Id { get; set; }

            [EntityProperty(Sorted = true)]
            public string AA { get; set; }

            [EntityProperty(Sorted = true)]
            public int AB { get; set; }
        }

        [MongoIndex(nameof(EntityB.BC)+ "." + nameof(EntityA.AA))]
        [MongoIndex(nameof(EntityB.BC) + "." + nameof(EntityA.AB))]
        [MongoIndex(nameof(EntityB.BD) + "." + nameof(EntityA.AA))]
        [MongoIndex(nameof(EntityB.BD) + "." + nameof(EntityA.AB))]
        [Entity(Scope = "MongoQueries", NamingPolicy = EntityNamingPolicy.LowerCase, Table = "queriestest2")]
        public class EntityB
        {
            [AutoId]
            public ObjectId Id { get; set; }

            [EntityProperty(Sorted = true)]
            public string BA { get; set; }

            [EntityProperty(Sorted = true)]
            public string[] BB { get; set; }

            [EntityProperty(Sorted = true)]
            public EntityA BC { get; set; }

            [EntityProperty(Sorted = true)]
            public EntityA[] BD { get; set; }
        }

        public class Fixture : MongoConnectionFixtureBase
        {
            private static void Drop(MongoConnection connection)
            {
                using (var query = connection.GetDeleteListQuery<EntityA>())
                    query.Execute();
                using (var query = connection.GetDeleteListQuery<EntityB>())
                    query.Execute();
            }

            protected override void ConfigureConnection(MongoConnection connection)
            {
                Drop(connection);
                base.ConfigureConnection(connection);
            }
        }

        private readonly Fixture mFixture;

        public MongoQueries(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static IEnumerable<object[]> ConnectionNames() => SqlConnectionSources.MongoConnectionNames();

        [TestOrder(1)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Create(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var schema = connection.GetSchema();
            schema.Should()
                .NotContain("queriestest1")
                .And.NotContain("queriestest2");

            using (var query = connection.GetCreateListQuery<EntityA>())
                query.Execute();
            using (var query = connection.GetCreateListQuery<EntityB>())
                query.Execute();

            schema = connection.GetSchema();
            schema.Should()
                .Contain("queriestest1")
                .And.Contain("queriestest2");
        }

        private static List<EntityA> CreateStageToUpdate(MongoConnection connection)
        {
            using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
                query.Execute();

            List<EntityA> list = new List<EntityA>();

            for (int i = 0; i < 10; i++)
            {
                list.Add(new EntityA()
                {
                    AA = $"value {i}",
                    AB = i
                });
            }

            using (var query = connection.GetInsertEntityQuery<EntityA>())
                query.Execute(list);

            return list;
        }

        [TestOrder(10)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void InsertOne(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
                query.Execute();

            var a = new EntityA()
            {
                AA = "aavalue",
                AB = 123
            };

            using (var query = connection.GetInsertEntityQuery<EntityA>())
                query.Execute(a);

            a.Id.Should().NotBe(ObjectId.Empty);

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                query.Where.Property(nameof(EntityA.Id)).Eq(a.Id);
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(1);
                all[0].Id.Should().Be(a.Id);
                all[0].AA.Should().Be("aavalue");
                all[0].AB.Should().Be(123);
            }
        }

        [TestOrder(11)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void InsertMany(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var list = CreateStageToUpdate(connection);

            list.Should()
                .HaveAllElementsMatching(e => e.Id != ObjectId.Empty);

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                query.AddOrderBy(nameof(EntityA.AB));
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(10);
                all.Should().BeInAscendingOrder(a => a.AB);

                for (int i = 0; i < all.Count; i++)
                {
                    var e = list.Find(x => x.Id == all[i].Id);
                    e.Should().NotBeNull();
                    e.AA.Should().Be(list[i].AA);
                    e.AB.Should().Be(list[i].AB);
                }
            }
        }

        [TestOrder(12)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void UpdateOne(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var list = CreateStageToUpdate(connection);

            list[0].AA = "newaa";
            list[0].AB = 100;

            using (var query = connection.GetUpdateEntityQuery<EntityA>())
                query.Execute(list[0]);

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(10);
                for (int i = 0; i < all.Count; i++)
                {
                    var e = list.Find(x => x.Id == all[i].Id);
                    e.Should().NotBeNull();
                    e.AA.Should().Be(all[i].AA);
                    e.AB.Should().Be(all[i].AB);
                }
            }
        }

        [TestOrder(13)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void UpdateMany_ByList(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var list = CreateStageToUpdate(connection);

            list[0].AA = "newaa";
            list[0].AB = 100;

            list[1].AA = "newaa 1";
            list[1].AB = 101;

            using (var query = connection.GetUpdateEntityQuery<EntityA>())
                query.Execute(new[] { list[0], list[1] });

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(10);
                for (int i = 0; i < all.Count; i++)
                {
                    var e = list.Find(x => x.Id == all[i].Id);
                    e.Should().NotBeNull();
                    e.AA.Should().Be(all[i].AA);
                    e.AB.Should().Be(all[i].AB);
                }
            }
        }

        [TestOrder(14)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void UpdateMany_ByCondition(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            CreateStageToUpdate(connection);

            using (var query = connection.GetUpdateMultiEntityQuery<EntityA>())
            {
                query.Where.Property(nameof(EntityA.AB)).In(1, 3, 5, 7, 9, 11);
                query.Set(nameof(EntityA.AA), "odd");
                query.Execute();
            }

            using (var query = connection.GetUpdateMultiEntityQuery<EntityA>())
            {
                query.Where.Property(nameof(EntityA.AB)).In(0, 2, 4, 6, 8, 10);
                query.Set(nameof(EntityA.AA), "even");
                query.Execute();
            }

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(10);
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].AB % 2 == 0)
                        all[i].AA.Should().Be("even");
                    else
                        all[i].AA.Should().Be("odd");
                }
            }
        }

        [TestOrder(15)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void UpdateComplexPath(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDeleteMultiEntityQuery<EntityB>())
                query.Execute();

            var b = new EntityB()
            {
                BA = "nameb",
                BB = new string[] { "v1", "v2", "v3" },
                BC = new EntityA
                {
                    AA = "namea1",
                    AB = 123
                },
                BD = new EntityA[]
                {
                    new EntityA
                    {
                        AA = "namea2",
                        AB = 456
                    }
                }
            };

            using (var query = connection.GetInsertEntityQuery<EntityB>())
                query.Execute(b);

            using (var query = connection.GetUpdateMultiEntityQuery<EntityB>())
            {
                query.Where.Property(nameof(EntityB.Id)).Eq(b.Id);
                query.Set("BB.1", "new_v2");
                query.Execute();
            }

            using (var query = connection.GetUpdateMultiEntityQuery<EntityB>())
            {
                query.Where.Property(nameof(EntityB.Id)).Eq(b.Id);
                query.Set("BC.AB", 789);
                ((Action)(() => query.Set("BC.AA", "123"))).Should().Throw<InvalidOperationException>();
                query.Execute();
            }

            using (var query = connection.GetUpdateMultiEntityQuery<EntityB>())
            {
                query.Where.Property(nameof(EntityB.Id)).Eq(b.Id);
                query.Set("BD.0.AA", "newnamea2");
                query.Execute();
            }

            using (var query = connection.GetSelectQuery<EntityB>())
            {
                query.Where.Property(nameof(EntityB.Id)).Eq(b.Id);
                var all = query.ReadAll<EntityB>();
                all.Should().HaveCount(1);
                var b1 = all[0];

                b1.BA.Should().Be("nameb");
                b1.BB[0].Should().Be("v1");
                b1.BB[1].Should().Be("new_v2");
                b1.BB[2].Should().Be("v3");
                b1.BC.AA.Should().Be("namea1");
                b1.BC.AB.Should().Be(789);
                b1.BD[0].AA.Should().Be("newnamea2");
                b1.BD[0].AB.Should().Be(456);
            }
        }

        [TestOrder(15)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void DeleteOne(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var list = CreateStageToUpdate(connection);

            using (var query = connection.GetDeleteEntityQuery<EntityA>())
                query.Execute(list[0]);

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(9);

                all.Should().HaveNoElementMatching(e => e.Id == list[0].Id);
            }
        }

        [TestOrder(16)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void DeleteMany_ByList(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var list = CreateStageToUpdate(connection);

            using (var query = connection.GetDeleteEntityQuery<EntityA>())
                query.Execute(new[] { list[0], list[1] });

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();

                all.Should().HaveCount(8);

                all.Should()
                    .HaveNoElementMatching(e => e.Id == list[0].Id)
                    .And.HaveNoElementMatching(e => e.Id == list[1].Id);
            }
        }

        [TestOrder(17)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void DeleteMany_ByCondition(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            CreateStageToUpdate(connection);

            using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
            {
                query.Where.Property(nameof(EntityA.AB)).Ge(5);
                query.Execute();
            }

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Count.Should().BeLessThan(10);
                all.Should().HaveNoElementMatching(e => e.AB >= 5);
            }
        }

        [TestOrder(18)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void UpdateMany_IgnoreNonExisting(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
                query.Execute();

            var list = CreateStageToUpdate(connection);

            list[0].AA = "newaa";
            list[0].AB = 100;

            list.Add(new EntityA()
            {
                AA = "newaa 1",
                AB = 101,
            });

            using (var query = connection.GetUpdateEntityQuery<EntityA>())
            {
                query.InsertIfNotExists = false;
                query.Execute(new[] { list[0], list[^1] });
            }

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(10);
                for (int i = 0; i < all.Count; i++)
                {
                    var e = list.Find(x => x.Id == all[i].Id);
                    e.Should().NotBeNull();
                    e.AA.Should().Be(all[i].AA);
                    e.AB.Should().Be(all[i].AB);
                }
                all.Should().HaveNoElementMatching(x => x.AA == "newaa 1");
            }
        }

        [TestOrder(19)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void UpdateMany_InsertNonExisting(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
                query.Execute();

            var list = CreateStageToUpdate(connection);

            list[0].AA = "newaa";
            list[0].AB = 100;

            list.Add(new EntityA()
            {
                AA = "newaa 1",
                AB = 101,
            });

            using (var query = connection.GetUpdateEntityQuery<EntityA>())
            {
                query.InsertIfNotExists = true;
                query.Execute(new[] { list[0], list[^1] });
            }

            using (var query = connection.GetSelectQuery<EntityA>())
            {
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(11);
                for (int i = 0; i < all.Count; i++)
                {
                    var e = list.Find(x => x.Id == all[i].Id);
                    e.Should().NotBeNull();
                    e.AA.Should().Be(all[i].AA);
                    e.AB.Should().Be(all[i].AB);
                }
                all.Should().HaveElementMatching(x => x.AA == "newaa 1");
            }
        }

        private MongoConnection PrepareSelectData(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);
            var connection = mFixture.GetInstance(connectionName);
            if (!connection.Tags.GetTag<bool>("testSetCreated"))
            {
                using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
                    query.Execute();
                using (var query = connection.GetDeleteMultiEntityQuery<EntityB>())
                    query.Execute();

                var list1 = new List<EntityA>();

                for (int i = 0; i < 10; i++)
                {
                    list1.Add(new EntityA()
                    {
                        AA = $"value {i}",
                        AB = i
                    });
                }

                using (var query = connection.GetInsertEntityQuery<EntityA>())
                    query.Execute(list1);

                var list2 = new List<EntityB>();

                for (int i = 0; i < 20; i++)
                {
                    list2.Add(new EntityB()
                    {
                        BA = $"value {i}",
                        BB = new string[] { $"value {i} 1", $"value {2} 1", $"value {3} 1" },
                        BC = new EntityA()
                        {
                            AA = $"value {i * 10}",
                            AB = i * 10,
                        }
                    });
                }

                using (var query = connection.GetInsertEntityQuery<EntityB>())
                    query.Execute(list2);
            }
            return connection;
        }

        [TestOrder(30)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Count(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.GetCountQuery<EntityA>())
                query.RowCount.Should().Be(10);

            using (var query = connection.GetCountQuery<EntityB>())
                query.RowCount.Should().Be(20);
        }

        [TestOrder(31)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Everything(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.GetSelectQuery<EntityB>())
            {
                var all = query.ReadAll<EntityB>();
                all.Should().HaveCount(20);
            }
        }

        [TestOrder(100)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Drop(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var schema = connection.GetSchema();

            schema.Should()
                .Contain("queriestest1")
                .And.Contain("queriestest2");

            using (var query = connection.GetDeleteListQuery<EntityA>())
                query.Execute();
            using (var query = connection.GetDeleteListQuery<EntityB>())
                query.Execute();

            schema = connection.GetSchema();
            schema.Should()
                .NotContain("queriestest1")
                .And.NotContain("queriestest2");
        }
    }
}

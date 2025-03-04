using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Bson;
using Xunit;

namespace Gehtsoft.EF.Test.Mongo
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class MongoQueries_ViaContext : IClassFixture<MongoQueries_ViaContext.Fixture>
    {
        [Entity(Scope = "MongoQueries", NamingPolicy = EntityNamingPolicy.LowerCase, Table = "queriestest4")]
        public class EntityA
        {
            [AutoId]
            public ObjectId Id { get; set; }

            [EntityProperty(Sorted = true)]
            public string AA { get; set; }

            [EntityProperty(Sorted = true)]
            public int AB { get; set; }
        }

        [MongoIndex(nameof(EntityB.BC) + "." + nameof(EntityA.AA))]
        [MongoIndex(nameof(EntityB.BC) + "." + nameof(EntityA.AB))]
        [MongoIndex(nameof(EntityB.BD) + "." + nameof(EntityA.AA))]
        [MongoIndex(nameof(EntityB.BD) + "." + nameof(EntityA.AB))]
        [Entity(Scope = "MongoQueries", NamingPolicy = EntityNamingPolicy.LowerCase, Table = "queriestest5")]
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
            new public IEntityContext GetInstance(string connection) => GetInstance(connection, AppConfiguration.Instance.Get("nosqlConnections:" + connection));

            new public IEntityContext GetInstance(string connectionName, string connectionString) => base.GetInstance(connectionName, connectionString);

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

        public MongoQueries_ViaContext(Fixture fixture)
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

            var schema = connection.ExistingTables();
            schema.Should()
                .NotContain(e => e.Name == "queriestest4")
                .And.NotContain(e => e.Name == "queriestest5");

            using (var query = connection.CreateEntity<EntityA>())
                query.Execute();
            using (var query = connection.CreateEntity<EntityB>())
                query.Execute();

            schema = connection.ExistingTables();
            schema.Should()
                .Contain(e => e.Name == "queriestest4")
                .And.Contain(e => e.Name == "queriestest5");
        }

        private static List<EntityA> CreateStageToUpdate(IEntityContext connection)
        {
            using (var query = connection.DeleteMultiple<EntityA>())
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

            using (var query = connection.InsertEntity<EntityA>())
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

            using (var query = connection.DeleteMultiple<EntityA>())
                query.Execute();

            var a = new EntityA()
            {
                AA = "aavalue",
                AB = 123
            };

            using (var query = connection.InsertEntity<EntityA>())
                query.Execute(a);

            a.Id.Should().NotBe(ObjectId.Empty);

            using (var query = connection.Select<EntityA>())
            {
                query.Where.Property(nameof(EntityA.Id)).Eq(a.Id);
                query.Execute();
                var all = query.ReadAll<EntityA>();
                all.Should().HaveCount(1);
                all[0].Id.Should().Be(a.Id);
                all[0].AA.Should().Be("aavalue");
                all[0].AB.Should().Be(123);
            }
        }

        [TestOrder(10)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public async Task InsertOneAsync(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.DeleteMultiple<EntityA>())
                await query.ExecuteAsync();

            var a = new EntityA()
            {
                AA = "aavalue",
                AB = 123
            };

            using (var query = connection.InsertEntity<EntityA>())
                await query.ExecuteAsync(a);

            a.Id.Should().NotBe(ObjectId.Empty);

            using (var query = connection.Select<EntityA>())
            {
                query.Where.Property(nameof(EntityA.Id)).Eq(a.Id);
                await query.ExecuteAsync();
                var all = await query.ReadAllAsync<EntityA>();
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

            using (var query = connection.Select<EntityA>())
            {
                query.Order.Add(nameof(EntityA.AB));
                query.Execute();
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

            using (var query = connection.UpdateEntity<EntityA>())
                query.Execute(list[0]);

            using (var query = connection.Select<EntityA>())
            {
                query.Execute();
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

            using (var query = connection.UpdateEntity<EntityA>())
                query.Execute(new[] { list[0], list[1] });

            using (var query = connection.Select<EntityA>())
            {
                query.Execute();
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

        [TestOrder(15)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void DeleteOne(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var list = CreateStageToUpdate(connection);

            using (var query = connection.DeleteEntity<EntityA>())
                query.Execute(list[0]);

            using (var query = connection.Select<EntityA>())
            {
                query.Execute();
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

            using (var query = connection.DeleteEntity<EntityA>())
                query.Execute(new[] { list[0], list[1] });

            using (var query = connection.Select<EntityA>())
            {
                query.Execute();
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

            using (var query = connection.DeleteMultiple<EntityA>())
            {
                query.Where.Property(nameof(EntityA.AB)).Ge(5);
                query.Execute();
            }

            using (var query = connection.Select<EntityA>())
            {
                query.Execute();
                var all = query.ReadAll<EntityA>();
                all.Count.Should().BeLessThan(10);
                all.Should().HaveNoElementMatching(e => e.AB >= 5);
            }
        }

        private IEntityContext PrepareSelectData(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                Create(connectionName);
            var connection = mFixture.GetInstance(connectionName) as MongoConnection;
            if (!connection.Tags.GetTag<bool>("testSetCreated"))
            {
                using (var query = connection.GetDeleteMultiEntityQuery<EntityA>())
                    query.Execute();
                using (var query = connection.GetDeleteMultiEntityQuery<EntityB>())
                    query.Execute();

                var list1 = new List<EntityA>();

                for (int i = 0; i < 50; i++)
                {
                    list1.Add(new EntityA()
                    {
                        AA = $"value {i / 10}",
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
                        },
                        BD = new EntityA[]
                        {
                            new EntityA()
                            {
                                AA = $"value {i * 20}",
                                AB = i * 10 + 1,
                            },
                                new EntityA()
                            {
                                AA = $"value {i * 30}",
                                AB = i * 10 + 2,
                            },
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

            using (var query = connection.Count<EntityA>())
                query.GetCount().Should().Be(50);

            using (var query = connection.Count<EntityB>())
                query.GetCount().Should().Be(20);
        }

        [TestOrder(30)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public async Task Select_CountAsync(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Count<EntityA>())
                (await query.GetCountAsync()).Should().Be(50);

            using (var query = connection.Count<EntityB>())
                (await query.GetCountAsync()).Should().Be(20);
        }

        [TestOrder(31)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Everything(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityB>())
            {
                query.Execute();
                var all = query.ReadAll<EntityB>();
                all.Should().HaveCount(20);
            }
        }

        [TestOrder(32)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_OrderBy(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityA>())
            {
                query.Order.Add(nameof(EntityA.AB));
                query.Execute();
                var all = query.ReadAll<EntityA>();
                all.Should().BeInAscendingOrder(a => a.AB);
            }
        }

        [TestOrder(33)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_OrderBy1(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityA>())
            {
                query.Order.Add(nameof(EntityA.AA));
                query.Order.Add(nameof(EntityA.AB), SortDir.Desc);
                query.Execute();
                var all = query.ReadAll<EntityA>();
                for (int i = 1; i < all.Count; i++)
                {
                    var rc = all[i].AA.CompareTo(all[i - 1].AA);
                    rc.Should().BeGreaterThanOrEqualTo(0, "AA must be in ascending order");
                    if (rc == 0)
                        all[i].AB.Should().BeLessThan(all[i - 1].AB, "AB must be in descending order");
                }
            }
        }

        [TestOrder(33)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_OrderBy2(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityA>())
            {
                query.Order.Add(nameof(EntityA.AA), SortDir.Desc);
                query.Order.Add(nameof(EntityA.AB));
                query.Execute();
                var all = query.ReadAll<EntityA>();
                for (int i = 1; i < all.Count; i++)
                {
                    var rc = all[i].AA.CompareTo(all[i - 1].AA);
                    rc.Should().BeLessThanOrEqualTo(0, "AA must be in descending order");
                    if (rc == 0)
                        all[i].AB.Should().BeGreaterThan(all[i - 1].AB, "AB must be in ascending order");
                }
            }
        }

        [TestOrder(34)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_TakeSkip(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            EntityCollection<EntityA> all;
            using (var query = connection.Select<EntityA>())
            {
                query.Order.Add(nameof(EntityA.AB));
                query.Execute();
                all = query.ReadAll<EntityA>();
            }

            using (var query = connection.Select<EntityA>())
            {
                query.Skip = 2;
                query.Take = 3;
                query.Execute();
                var part = query.ReadAll<EntityA>();
                part.Should().HaveCount(3);
                part[0].Id.Should().Be(all[2].Id);
                part[1].Id.Should().Be(all[3].Id);
                part[2].Id.Should().Be(all[4].Id);
            }
        }

        [TestOrder(38)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Where_Simple(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityB>())
            {
                query.Where.Property(nameof(EntityB.BA)).Like("/.+2.*/");
                query.Execute();
                var all = query.ReadAll<EntityB>();
                all.Should()
                    .NotBeEmpty()
                    .And.HaveAllElementsMatching(e => e.BA.Contains("2"));
            }
        }

        [TestOrder(38)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Where_SimpleOfProperty(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityB>())
            {
                query.Where.Property("BC.AA").Like("%2%");
                query.Execute();
                var all = query.ReadAll<EntityB>();
                all.Should()
                    .NotBeEmpty()
                    .And.HaveAllElementsMatching(e => e.BC.AA.Contains("2"));
            }
        }

        [TestOrder(38)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Where_LogOp_Connected(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityB>())
            {
                query.Where.Or().Property(nameof(EntityB.BA)).Like("/.+2.*/");
                query.Where.Or().Property(nameof(EntityB.BA)).Like("/.+3.*/");
                query.Execute();
                var all = query.ReadAll<EntityB>();
                all.Should()
                    .NotBeEmpty()
                    .And.HaveAllElementsMatching(e => e.BA.Contains("2") || e.BA.Contains("3"));
            }
        }

        [TestOrder(38)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Where_Where_One_Array_Item(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityB>())
            {
                query.Where.Property("BD.1.AB").Eq(22);
                query.Execute();
                var all = query.ReadAll<EntityB>();
                all.Should()
                    .HaveCount(1)
                    .And.HaveElementMatchingAt(0, e => e.BD[1].AB == 22);
            }
        }

        [TestOrder(38)]
        [Theory]
        [MemberData(nameof(ConnectionNames))]
        public void Select_Where_Where_Any_Array_Item(string connectionName)
        {
            var connection = PrepareSelectData(connectionName);

            using (var query = connection.Select<EntityB>())
            {
                query.Where.Property("BD.AB").Ge(20);
                query.Where.Property("BD.AB").Ls(50);
                query.Execute();
                var all = query.ReadAll<EntityB>();
                all.Should()
                    .HaveCount(3)
                    .And.HaveAllElementsMatching(e => e.BD[0].AB >= 20 && e.BD[1].AB < 50);
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

            var schema = connection.ExistingTables();

            schema.Should()
                .Contain(e => e.Name == "queriestest4")
                .And.Contain(e => e.Name == "queriestest5");

            using (var query = connection.DropEntity<EntityA>())
                query.Execute();
            using (var query = connection.DropEntity<EntityB>())
                query.Execute();

            schema = connection.ExistingTables();
            schema.Should()
                .NotContain(e => e.Name == "queriestest4")
                .And.NotContain(e => e.Name == "queriestest5");
        }
    }
}

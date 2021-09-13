using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using MongoDB.Bson.Serialization.Conventions;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class QueryiesOnDb_GenericAccessor : IClassFixture<QueryiesOnDb_GenericAccessor.Fixture>
    {
        private const string mFlags = "+sqlite";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.ConnectionNames(flags, mFlags);

        [Entity(Scope = "genericaccessor", Table = "ge_dict")]
        public class Dict
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Field = "n", Size = 32)]
            public string Name { get; set; }
        }

        public class DictFilter : GenericEntityAccessorFilterT<Dict>
        {
            [FilterProperty(Operation = CmpOp.Eq)]
            public int? Id { get; set; }

            [FilterProperty(Operation = CmpOp.Like, PropertyName = nameof(Dict.Name))]
            public string Name { get; set; }
        }

        [Entity(Scope = "genericaccessor", Table = "ge_dict1")]
        public class Dict1
        {
            [EntityProperty(PrimaryKey = true)]
            public Guid Id { get; set; }

            [EntityProperty(Field = "n", Size = 32)]
            public string Name { get; set; }
        }

        public class Dict1Filter : GenericEntityAccessorFilterT<Dict1>
        {
            [FilterProperty(Operation = CmpOp.Eq)]
            public Guid? Id { get; set; }

            [FilterProperty(Operation = CmpOp.Like)]
            public string Name { get; set; }
        }

        [Entity(Scope = "genericaccessor", Table = "ge_entity")]
        public class Entity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Field = "n", Size = 32)]
            public string Name { get; set; }

            [ForeignKey]
            public Dict Dict { get; set; }

            [EntityProperty]
            public int Value { get; set; }

            [EntityProperty]
            public DateTime Date { get; set; }
        }

        public class EntityFilter : GenericEntityAccessorFilterT<Entity>
        {
            [FilterProperty(Operation = CmpOp.In, PropertyName = nameof(Entity.Id))]
            public List<int> Ids { get; set; }

            [FilterProperty(Operation = CmpOp.Eq)]
            public Dict Dict { get; set; }

            [FilterProperty(Operation = CmpOp.Like)]
            public string Name { get; set; }

            public int? ValueFrom { get; set; }

            public int? ValueTo { get; set; }

            protected override void BindToQueryImpl(ConditionEntityQueryBase query)
            {
                if (ValueFrom != null)
                    query.Where.Property(nameof(Entity.Value)).Ge(ValueFrom.Value);
                if (ValueTo != null)
                    query.Where.Property(nameof(Entity.Value)).Le(ValueTo.Value);

                base.BindToQueryImpl(query);
            }
        }

        [Entity(Scope = "genericaccessor", Table = "ge_aggregator")]
        public class Aggregator
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Value { get; set; }
        }

        public class AggregatorFilter : GenericEntityAccessorFilterT<Aggregator>
        {
            [FilterProperty(Operation = CmpOp.Ge, PropertyName = nameof(Aggregator.Value))]
            public int? ValueFrom { get; set; }
            [FilterProperty(Operation = CmpOp.Le, PropertyName = nameof(Aggregator.Value))]
            public int? ValueTo { get; set; }
        }

        [Entity(Scope = "genericaccessor", Table = "ge_aggregat")]
        public class Aggregat
        {
            public bool Delete { get; set; }

            [AutoId]
            public int Id { get; set; }

            [ForeignKey]
            public Aggregator Aggregator { get; set; }

            [EntityProperty]
            public int Value { get; set; }
        }

        public class Fixture : ConnectionFixtureBase
        {
            public bool DeleteOnDispose { get; } = true;

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
                using (var query = connection.GetCreateEntityQuery<Dict1>())
                    query.Execute();

                using (var query = connection.GetCreateEntityQuery<Dict>())
                    query.Execute();

                using (var query = connection.GetCreateEntityQuery<Entity>())
                    query.Execute();

                using (var query = connection.GetCreateEntityQuery<Aggregator>())
                    query.Execute();

                using (var query = connection.GetCreateEntityQuery<Aggregat>())
                    query.Execute();

                base.ConfigureConnection(connection);
            }

            protected override void TearDownConnection(SqlDbConnection connection)
            {
                if (DeleteOnDispose)
                    Drop(connection);
                base.TearDownConnection(connection);
            }

            private static void Drop(SqlDbConnection connection)
            {
                using (var query = connection.GetDropEntityQuery<Entity>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Dict>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Dict1>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Aggregat>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Aggregator>())
                    query.Execute();
            }
        }

        private readonly Fixture mFixture;

        public QueryiesOnDb_GenericAccessor(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Save_AutoId(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var oldCount = accessor.Count(null);

            Dict dict = new Dict()
            {
                Name = "newItem"
            };

            accessor.Save(dict);

            dict.Id.Should().BeGreaterThan(0);
            accessor.Count(null).Should().Be(oldCount + 1);

            accessor.Save(dict);
            accessor.Count(null).Should().Be(oldCount + 1);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Save_AutoId_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var oldCount = await accessor.CountAsync(null);

            Dict dict = new Dict()
            {
                Name = "newItem"
            };

            await accessor.SaveAsync(dict);

            dict.Id.Should().BeGreaterThan(0);
            accessor.Count(null).Should().Be(oldCount + 1);

            await accessor.SaveAsync(dict);
            (await accessor.CountAsync(null)).Should().Be(oldCount + 1);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Save_GuidId(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict1, Guid>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new Dict1Filter()));

            var oldCount = accessor.Count(null);

            Dict1 dict = new Dict1()
            {
                Name = "newItem"
            };

            accessor.Save(dict);

            dict.Id.Should().NotBe(Guid.Empty);
            accessor.Count(null).Should().Be(oldCount + 1);

            accessor.Save(dict);
            accessor.Count(null).Should().Be(oldCount + 1);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Save_GuidId_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict1, Guid>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new Dict1Filter()));

            var oldCount = await accessor.CountAsync(null);

            Dict1 dict = new Dict1()
            {
                Name = "newItem"
            };

            await accessor.SaveAsync(dict);

            dict.Id.Should().NotBe(Guid.Empty);
            accessor.Count(null).Should().Be(oldCount + 1);

            await accessor.SaveAsync(dict);
            accessor.Count(null).Should().Be(oldCount + 1);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Get(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            Dict dict = new Dict()
            {
                Name = "newItem"
            };
            accessor.Save(dict);

            var dict1 = accessor.Get(dict.Id);
            dict1.Id.Should().Be(dict.Id);
            dict1.Name.Should().Be(dict.Name);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Get_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            Dict dict = new Dict()
            {
                Name = "newItem"
            };
            await accessor.SaveAsync(dict);

            var dict1 = await accessor.GetAsync(dict.Id);
            dict1.Id.Should().Be(dict.Id);
            dict1.Name.Should().Be(dict.Name);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Delete(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            Dict dict = new Dict()
            {
                Name = "newItem"
            };
            accessor.Save(dict);
            var oldCount = accessor.Count(null);

            accessor.Delete(dict);
            accessor.Count(null).Should().Be(oldCount - 1);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Delete_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            Dict dict = new Dict()
            {
                Name = "newItem"
            };
            accessor.Save(dict);
            var oldCount = accessor.Count(null);

            await accessor.DeleteAsync(dict);

            accessor.Count(null).Should().Be(oldCount - 1);
        }

        private static List<Dict> CreateDict(GenericEntityAccessor<Dict, int> accessor, int count)
        {
            List<Dict> r = new List<Dict>();

            for (int i = 0; i < count; i++)
            {
                var dict = new Dict() { Name = $"Name {i}" };
                accessor.Save(dict);
                r.Add(dict);
            }
            return r;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var src = CreateDict(accessor, 10);
            var all = accessor.Read<List<Dict>>(null, null, null, null);
            all.Should().HaveCount(10);
            all.Should().HaveAllElementsMatching(m => src.Any(s => s.Id == m.Id && s.Name == m.Name));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task SelectAll_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var src = CreateDict(accessor, 10);
            var all = await accessor.ReadAsync<List<Dict>>(null, null, null, null);
            all.Should().HaveCount(10);
            all.Should().HaveAllElementsMatching(m => src.Any(s => s.Id == m.Id && s.Name == m.Name));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll_Limit(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var src = CreateDict(accessor, 10);
            var all = accessor.Read<EntityCollection<Dict>>(null, null, null, 5);
            all.Should().HaveCount(5);
            all.Should().HaveAllElementsMatching(m => src.Any(s => s.Id == m.Id && s.Name == m.Name));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll_SkipAndLimit(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var src = CreateDict(accessor, 10);
            var all = accessor.Read<EntityCollection<Dict>>(null, new[] { new GenericEntitySortOrder("Id") }, 5, 2);
            all.Should().HaveCount(2);
            all.Should().HaveAllElementsMatching(m => m.Id >= src[5].Id && m.Id < src[7].Id);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll_Filter(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            CreateDict(accessor, 20);
            var all = accessor.Read<EntityCollection<Dict>>(new DictFilter() { Name = "%5%" }, new[] { new GenericEntitySortOrder("Id") }, null, null);
            all.Should().NotBeEmpty();
            all.Should().HaveAllElementsMatching(m => m.Name.Contains('5'));

            accessor.Count(new DictFilter() { Name = "%5%" }).Should().Be(all.Count);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task SelectAll_Filter_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            CreateDict(accessor, 20);
            var all = await accessor.ReadAsync<EntityCollection<Dict>>(new DictFilter() { Name = "%5%" }, new[] { new GenericEntitySortOrder("Id") }, null, null);
            all.Should().NotBeEmpty();
            all.Should().HaveAllElementsMatching(m => m.Name.Contains('5'));

            (await accessor.CountAsync(new DictFilter() { Name = "%5%" })).Should().Be(all.Count);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CanDelete_Simple(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var src = CreateDict(accessor, 1);

            accessor.CanDelete(src[0]).Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task CanDelete_Simple_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var accessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => accessor.DeleteMultiple(new DictFilter()));

            var src = CreateDict(accessor, 1);

            (await accessor.CanDeleteAsync(src[0])).Should().BeTrue();
        }

        private static List<Entity> CreateEntity(GenericEntityAccessor<Entity, int> accessor, int count, List<Dict> dict, bool randomData)
        {
            List<Entity> r = new List<Entity>();
            Random random = new Random();

            for (int i = 0; i < count; i++)
            {
                var d1 = randomData ? random.Next(1, count * 10) : i + 1;
                var d2 = randomData ? random.Next(1, 100) : i + 1;
                var d3 = DateTime.Now.AddMinutes(randomData ? -random.Next(1, 360000) : -i * 3600);

                var e = new Entity()
                {
                    Name = $"Entity {d1}",
                    Value = d2,
                    Date = d3,
                    Dict = dict[i % dict.Count]
                };
                accessor.Save(e);
                r.Add(e);
            }
            return r;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CanDelete_WithDependencies(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            var entityAccessor = new GenericEntityAccessor<Entity, int>(connection);
            using var delayed = new DelayedAction(() =>
            {
                entityAccessor.DeleteMultiple(new EntityFilter());
                dictAccessor.DeleteMultiple(new DictFilter());
            });

            var dict = CreateDict(dictAccessor, 5);
            var entities = CreateEntity(entityAccessor, 10, dict, true);

            dictAccessor.CanDelete(dict[0]).Should().BeFalse();
            dictAccessor.CanDelete(dict[0], new[] { typeof(Entity) }).Should().BeTrue();

            for (int i = 0; i < entities.Count; i++)
                if (entities[i].Dict.Id == dict[0].Id)
                    entityAccessor.Delete(entities[i]);

            dictAccessor.CanDelete(dict[0]).Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll_Filter_Custom(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            var entityAccessor = new GenericEntityAccessor<Entity, int>(connection);
            using var delayed = new DelayedAction(() =>
            {
                entityAccessor.DeleteMultiple(new EntityFilter());
                dictAccessor.DeleteMultiple(new DictFilter());
            });

            var dict = CreateDict(dictAccessor, 5);
            CreateEntity(entityAccessor, 10, dict, false);

            var filter = new EntityFilter()
            {
                ValueFrom = 2,
                ValueTo = 7,
            };

            var count = entityAccessor.Count(filter);
            count.Should().Be(6);

            var all = entityAccessor.Read<List<Entity>>(filter, null, null, null);
            all.Should().HaveCount(6);
            all.Should().HaveAllElementsMatching(e => e.Value >= 2 && e.Value <= 7);

            entityAccessor.DeleteMultiple(filter);
            all = entityAccessor.Read<List<Entity>>(filter, null, null, null);
            all.Should().HaveCount(0);

            all = entityAccessor.Read<List<Entity>>(null, null, null, null);
            all.Should().NotBeEmpty();
            all.Should().HaveAllElementsMatching(e => e.Value < 2 || e.Value > 7);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task SelectAll_Filter_Custom_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            var entityAccessor = new GenericEntityAccessor<Entity, int>(connection);
            using var delayed = new DelayedAction(() =>
            {
                entityAccessor.DeleteMultiple(new EntityFilter());
                dictAccessor.DeleteMultiple(new DictFilter());
            });

            var dict = CreateDict(dictAccessor, 5);
            CreateEntity(entityAccessor, 10, dict, false);

            var filter = new EntityFilter()
            {
                ValueFrom = 2,
                ValueTo = 7,
            };

            var count = await entityAccessor.CountAsync(filter);
            count.Should().Be(6);

            var all = await entityAccessor.ReadAsync<List<Entity>>(filter, null, null, null);
            all.Should().HaveCount(6);
            all.Should().HaveAllElementsMatching(e => e.Value >= 2 && e.Value <= 7);

            await entityAccessor.DeleteMultipleAsync(filter);

            count = await entityAccessor.CountAsync(filter);
            count.Should().Be(0);

            all = await entityAccessor.ReadAsync<List<Entity>>(null, null, null, null);
            all.Should().NotBeEmpty();
            all.Should().HaveAllElementsMatching(e => e.Value < 2 || e.Value > 7);
        }

        public class DictUpdateEntityRecord : GenericEntityAccessorUpdateRecordT<Entity>
        {
            [UpdateRecordProperty(PropertyName = nameof(Entity.Dict))]
            public Dict Dict { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll_UpdateMany_ByRecord(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            var entityAccessor = new GenericEntityAccessor<Entity, int>(connection);
            using var delayed = new DelayedAction(() =>
            {
                entityAccessor.DeleteMultiple(new EntityFilter());
                dictAccessor.DeleteMultiple(new DictFilter());
            });

            var dict = CreateDict(dictAccessor, 3);
            CreateEntity(entityAccessor, 10, dict, false);

            List<int> ids = new List<int>();
            var all = entityAccessor.Read<List<Entity>>(null, null, null, null);
            foreach (var e in all)
            {
                if (e.Dict.Id == dict[0].Id)
                    ids.Add(e.Id);
            }

            ids.Should().NotBeEmpty();

            var filter = new EntityFilter()
            {
                Dict = dict[0],
            };

            entityAccessor.UpdateMultiple(filter, new DictUpdateEntityRecord() { Dict = dict[1] });

            all = entityAccessor.Read<List<Entity>>(null, null, null, null);
            all.Should().HaveAllElementsMatching(e => e.Dict.Id != dict[0].Id);

            filter = new EntityFilter()
            {
                Ids = ids
            };

            all = entityAccessor.Read<List<Entity>>(filter, null, null, null);

            all.Should().HaveCount(ids.Count);
            all.Should().HaveAllElementsMatching(e => e.Dict.Id == dict[1].Id);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectAll_UpdateMany_ByProperty(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            var entityAccessor = new GenericEntityAccessor<Entity, int>(connection);
            using var delayed = new DelayedAction(() =>
            {
                entityAccessor.DeleteMultiple(new EntityFilter());
                dictAccessor.DeleteMultiple(new DictFilter());
            });

            var dict = CreateDict(dictAccessor, 3);
            CreateEntity(entityAccessor, 10, dict, false);

            List<int> ids = new List<int>();
            var all = entityAccessor.Read<List<Entity>>(null, null, null, null);
            foreach (var e in all)
            {
                if (e.Dict.Id == dict[0].Id)
                    ids.Add(e.Id);
            }

            ids.Should().NotBeEmpty();

            var filter = new EntityFilter()
            {
                Dict = dict[0],
            };

            entityAccessor.UpdateMultiple(filter, nameof(Entity.Dict), dict[1]);

            all = entityAccessor.Read<List<Entity>>(null, null, null, null);
            all.Should().HaveAllElementsMatching(e => e.Dict.Id != dict[0].Id);

            filter = new EntityFilter()
            {
                Ids = ids
            };

            all = entityAccessor.Read<List<Entity>>(filter, null, null, null);

            all.Should().HaveCount(ids.Count);
            all.Should().HaveAllElementsMatching(e => e.Dict.Id == dict[1].Id);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Sequence_Reader(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            var entityAccessor = new GenericEntityAccessor<Entity, int>(connection);
            using var delayed = new DelayedAction(() =>
            {
                entityAccessor.DeleteMultiple(new EntityFilter());
                dictAccessor.DeleteMultiple(new DictFilter());
            });

            List<Entity> entities;
            using (var t = connection.BeginTransaction())
            {
                var dict = CreateDict(dictAccessor, 3);
                entities = CreateEntity(entityAccessor, 100, dict, true);

                for (int a = 0; a < 5; a++)
                {
                    for (int b = 0; b < 5; b++)
                    {
                        for (int c = 0; c < 5; c++)
                        {
                            Entity e1 = new Entity()
                            {
                                Name = $"SqName {a}",
                                Value = c,
                                Date = new DateTime(2010, 5, b + 1),
                                Dict = dict[c % dict.Count]
                            };
                            entityAccessor.Save(e1);
                            entities.Add(e1);
                        }
                    }
                }
                t.Commit();
            }

            entities.Sort((a, b) =>
            {
                var nc = a.Name.CompareTo(b.Name);
                if (nc > 0)
                    return 1;

                if (nc < 0)
                    return -1;

                if (a.Date > b.Date)
                    return 1;

                if (a.Date < b.Date)
                    return -1;

                if (a.Value > b.Value)
                    return 1;

                if (a.Value < b.Value)
                    return -1;

                return 0;
            });

            var order = new[] {
                new GenericEntitySortOrder(nameof(Entity.Name)),
                new GenericEntitySortOrder(nameof(Entity.Date)),
                new GenericEntitySortOrder(nameof(Entity.Value))
            };

            var e = (Entity)null;
            var k = 0;

            for (int i = 0; i < entities.Count; i++)
            {
                k = entityAccessor.NextKey(e, order, null, false);
                e = entityAccessor.NextEntity(e, order, null, false);

                e.Should().NotBeNull();
                e.Id.Should().Be(entities[i].Id);
                k.Should().Be(entities[i].Id);
            }

            k = entityAccessor.NextKey(e, order, null, false);
            e = entityAccessor.NextEntity(e, order, null, false);

            k.Should().BeLessOrEqualTo(0);
            e.Should().BeNull();

            e = (Entity)null;

            for (int i = entities.Count - 1; i >= 0; i--)
            {
                k = entityAccessor.NextKey(e, order, null, true);
                e = entityAccessor.NextEntity(e, order, null, true);

                e.Should().NotBeNull();
                e.Id.Should().Be(entities[i].Id);
                k.Should().Be(entities[i].Id);
            }

            k = entityAccessor.NextKey(e, order, null, true);
            e = entityAccessor.NextEntity(e, order, null, true);

            k.Should().BeLessOrEqualTo(0);
            e.Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Sequence_Reader_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var dictAccessor = new GenericEntityAccessor<Dict, int>(connection);
            using var delayed = new DelayedAction(() => dictAccessor.DeleteMultiple(new DictFilter()));

            var dict = CreateDict(dictAccessor, 15);

            dict.Sort((a, b) => a.Name.CompareTo(b.Name));

            var order = new[] {
                new GenericEntitySortOrder(nameof(Dict.Name)),
            };

            var e = (Dict)null;
            var k = 0;

            for (int i = 0; i < dict.Count; i++)
            {
                k = await dictAccessor.NextKeyAsync(e, order, null, false);
                e = await dictAccessor.NextEntityAsync(e, order, null, false);

                e.Should().NotBeNull();
                e.Id.Should().Be(dict[i].Id);
                k.Should().Be(dict[i].Id);
            }

            e = null;

            for (int i = dict.Count - 1; i >= 0; i--)
            {
                k = await dictAccessor.NextKeyAsync(e, order, null, true);
                e = await dictAccessor.NextEntityAsync(e, order, null, true);

                e.Should().NotBeNull();
                e.Id.Should().Be(dict[i].Id);
                k.Should().Be(dict[i].Id);
            }
        }

        private static void ClearAggregateStage(SqlDbConnection connection)
        {
            using (var query = connection.GetMultiDeleteEntityQuery<Aggregat>())
                query.Execute();
            using (var query = connection.GetMultiDeleteEntityQuery<Aggregator>())
                query.Execute();
        }

        private static void SetAggregateStage(SqlDbConnection connection)
        {
            for (int i = 0; i < 5; i++)
            {
                Aggregator agg = new Aggregator() { Value = i };
                using (var query = connection.GetInsertEntityQuery<Aggregator>())
                    query.Execute(agg);

                for (int j = 0; j < 5; j++)
                {
                    Aggregat agg1 = new Aggregat()
                    {
                        Aggregator = agg
                    };

                    using (var query = connection.GetInsertEntityQuery<Aggregat>())
                        query.Execute(agg1);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregator_Delete(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAggregateStage(connection));

            var aggregatorAccessor = new GenericEntityAccessorWithAggregates<Aggregator, int>(connection, typeof(Aggregat));

            SetAggregateStage(connection);

            var agg = aggregatorAccessor.Read<List<Aggregator>>(null, null, null, null);

            var aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(agg[1], null, null, null, null);

            aggt.Should()
                .HaveCount(5)
                .And.HaveAllElementsMatching(a => a.Aggregator.Id == agg[1].Id);

            aggregatorAccessor.GetAggregatesCount<Aggregat>(agg[1], null).Should().Be(5);

            aggregatorAccessor.GetAggregatesCount<Aggregat>(agg[1], null).Should().Be(5);

            connection.CanDelete<Aggregator>(agg[1]).Should().BeFalse();
            aggregatorAccessor.CanDelete(agg[1]).Should().BeTrue();

            aggregatorAccessor.Delete(agg[1]);
            aggregatorAccessor.Get(agg[1].Id).Should().BeNull();

            aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(agg[1], null, null, null, null);
            aggt.Should()
                .HaveCount(0);

            aggregatorAccessor.GetAggregatesCount<Aggregat>(agg[1], null).Should().Be(0);

            aggregatorAccessor.GetAggregatesCount<Aggregat>(agg[1], null).Should().Be(0);

            aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(agg[0], null, null, null, null);
            aggt.Should()
                .HaveCount(5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Aggregator_Delete_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAggregateStage(connection));

            var aggregatorAccessor = new GenericEntityAccessorWithAggregates<Aggregator, int>(connection, typeof(Aggregat));

            SetAggregateStage(connection);

            var agg = await aggregatorAccessor.ReadAsync<List<Aggregator>>(null, null, null, null);

            var aggt = await aggregatorAccessor.GetAggregatesAsync<List<Aggregat>, Aggregat>(agg[1], null, null, null, null);

            aggt.Should()
                .HaveCount(5)
                .And.HaveAllElementsMatching(a => a.Aggregator.Id == agg[1].Id);

            (await aggregatorAccessor.GetAggregatesCountAsync<Aggregat>(agg[1], null)).Should().Be(5);

            (await aggregatorAccessor.GetAggregatesCountAsync<Aggregat>(agg[1], null)).Should().Be(5);

            (await connection.CanDeleteAsync<Aggregator>(agg[1])).Should().BeFalse();
            (await aggregatorAccessor.CanDeleteAsync(agg[1])).Should().BeTrue();

            aggregatorAccessor.Delete(agg[1]);
            aggregatorAccessor.Get(agg[1].Id).Should().BeNull();

            aggt = await aggregatorAccessor.GetAggregatesAsync<List<Aggregat>, Aggregat>(agg[1], null, null, null, null);
            aggt.Should()
                .HaveCount(0);

            (await aggregatorAccessor.GetAggregatesCountAsync<Aggregat>(agg[1], null)).Should().Be(0);

            (await aggregatorAccessor.GetAggregatesCountAsync<Aggregat>(agg[1], null)).Should().Be(0);

            aggt = await aggregatorAccessor.GetAggregatesAsync<List<Aggregat>, Aggregat>(agg[0], null, null, null, null);
            aggt.Should()
                .HaveCount(5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregator_DeleteMultiple(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAggregateStage(connection));

            var aggregatorAccessor = new GenericEntityAccessorWithAggregates<Aggregator, int>(connection, typeof(Aggregat));
            var aggregatAccessor = new GenericEntityAccessor<Aggregat, int>(connection);

            SetAggregateStage(connection);
            var aggs = aggregatorAccessor.Read<List<Aggregator>>(null, null, null, null);

            var a2 = aggs.Find(a => a.Value == 2);
            var a3 = aggs.Find(a => a.Value == 3);

            aggregatorAccessor.DeleteMultiple(new AggregatorFilter() { ValueFrom = 2, ValueTo = 3 });

            aggregatorAccessor.Count(null).Should().Be(aggs.Count - 2);
            aggregatorAccessor.Get(a2.Id).Should().BeNull();
            aggregatorAccessor.Get(a3.Id).Should().BeNull();

            var aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(a2, null, null, null, null);
            aggt.Should()
                .HaveCount(0);

            aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(a3, null, null, null, null);
            aggt.Should()
                .HaveCount(0);

            aggregatAccessor.Count(null).Should().Be((aggs.Count - 2) * 5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Aggregator_DeleteMultiple_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAggregateStage(connection));

            var aggregatorAccessor = new GenericEntityAccessorWithAggregates<Aggregator, int>(connection, typeof(Aggregat));
            var aggregatAccessor = new GenericEntityAccessor<Aggregat, int>(connection);

            SetAggregateStage(connection);
            var aggs = aggregatorAccessor.Read<List<Aggregator>>(null, null, null, null);

            var a2 = aggs.Find(a => a.Value == 2);
            var a3 = aggs.Find(a => a.Value == 3);

            await aggregatorAccessor.DeleteMultipleAsync(new AggregatorFilter() { ValueFrom = 2, ValueTo = 3 });

            aggregatorAccessor.Count(null).Should().Be(aggs.Count - 2);
            aggregatorAccessor.Get(a2.Id).Should().BeNull();
            aggregatorAccessor.Get(a3.Id).Should().BeNull();

            var aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(a2, null, null, null, null);
            aggt.Should()
                .HaveCount(0);

            aggt = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(a3, null, null, null, null);
            aggt.Should()
                .HaveCount(0);

            aggregatAccessor.Count(null).Should().Be((aggs.Count - 2) * 5);
        }

        private static bool AggregatEquals(Aggregat a, Aggregat b)
            => a.Id == b.Id && a.Aggregator.Id == b.Aggregator.Id && a.Value == b.Value;

        private static bool AggregatIsNew(Aggregat a)
            => a.Id < 1;
        private static bool AggregatIsDefined(Aggregat a)
            => !a.Delete;

        private static bool AggregatIdEquals(Aggregat a, Aggregat b)
            => a.Id == b.Id;

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Aggregator_Save_FullScenario(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAggregateStage(connection));

            var aggregatorAccessor = new GenericEntityAccessorWithAggregates<Aggregator, int>(connection, typeof(Aggregat));
            var aggregatAccessor = new GenericEntityAccessor<Aggregat, int>(connection);

            var aggregator = new Aggregator();
            aggregatorAccessor.Save(aggregator);

            for (int i = 0; i < 5; i++)
            {
                var aggregat = new Aggregat()
                {
                    Aggregator = aggregator,
                    Value = i + 1,
                };
                aggregatAccessor.Save(aggregat);
            }

            var currentState = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(aggregator, null, null, null, null);
            var newState = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(aggregator, null, null, null, null);
            currentState.Sort((a, b) => a.Id.CompareTo(b.Id));
            newState.Sort((a, b) => a.Id.CompareTo(b.Id));

            //remove element by deleting from state
            newState.RemoveAt(0);
            //remove element by ignoring it
            newState[1].Delete = true;

            //change element
            newState[2].Value = 100;

            var newItem = new Aggregat() { Aggregator = aggregator, Value = 15 };
            //add element
            newState.Add(newItem);

            aggregatorAccessor.SaveAggregates<Aggregat>(aggregator, currentState, newState,
                AggregatEquals, AggregatIdEquals, AggregatIsDefined, AggregatIsNew);

            newItem.Id.Should().BeGreaterThan(0);

            var savedState = aggregatorAccessor.GetAggregates<List<Aggregat>, Aggregat>(aggregator, null, null, null, null);

            savedState.Should().HaveCount(4);

            savedState.Should().HaveNoElementMatching(e => e.Id == currentState[0].Id);
            savedState.Should().HaveNoElementMatching(e => e.Id == currentState[2].Id);
            savedState.Should().HaveElementMatching(e => e.Id == newItem.Id);
            savedState.Find(e => e.Id == newState[2].Id).Value.Should().Be(100);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task Aggregator_Save_FullScenario_Async(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAggregateStage(connection));

            var aggregatorAccessor = new GenericEntityAccessorWithAggregates<Aggregator, int>(connection, typeof(Aggregat));
            var aggregatAccessor = new GenericEntityAccessor<Aggregat, int>(connection);

            var aggregator = new Aggregator();
            await aggregatorAccessor.SaveAsync(aggregator);

            for (int i = 0; i < 5; i++)
            {
                var aggregat = new Aggregat()
                {
                    Aggregator = aggregator,
                    Value = i + 1,
                };
                await aggregatAccessor.SaveAsync(aggregat);
            }

            var currentState = await aggregatorAccessor.GetAggregatesAsync<List<Aggregat>, Aggregat>(aggregator, null, null, null, null);
            var newState = await aggregatorAccessor.GetAggregatesAsync<List<Aggregat>, Aggregat>(aggregator, null, null, null, null);
            currentState.Sort((a, b) => a.Id.CompareTo(b.Id));
            newState.Sort((a, b) => a.Id.CompareTo(b.Id));

            //remove element by deleting from state
            newState.RemoveAt(0);
            //remove element by ignoring it
            newState[1].Delete = true;

            //change element
            newState[2].Value = 100;

            var newItem = new Aggregat() { Aggregator = aggregator, Value = 15 };
            //add element
            newState.Add(newItem);

            await aggregatorAccessor.SaveAggregatesAsync<Aggregat>(aggregator, currentState, newState,
                AggregatEquals, AggregatIdEquals, AggregatIsDefined, AggregatIsNew);

            newItem.Id.Should().BeGreaterThan(0);

            var savedState = await aggregatorAccessor.GetAggregatesAsync<List<Aggregat>, Aggregat>(aggregator, null, null, null, null);

            savedState.Should().HaveCount(4);

            savedState.Should().HaveNoElementMatching(e => e.Id == currentState[0].Id);
            savedState.Should().HaveNoElementMatching(e => e.Id == currentState[2].Id);
            savedState.Should().HaveElementMatching(e => e.Id == newItem.Id);
            savedState.Find(e => e.Id == newState[2].Id).Value.Should().Be(100);
        }
    }
}
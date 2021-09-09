using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Metadata;
using DotLiquid;
using DotLiquid.Util;
using FluentAssertions;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlDb.Factory;
using Gehtsoft.EF.Test.Utils;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class QueryiesOnDb_UpdateAndBasicSelect : IClassFixture<QueryiesOnDb_UpdateAndBasicSelect.Fixture>
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.ConnectionNames(flags, mFlags);

        private readonly Fixture mFixture;

        public QueryiesOnDb_UpdateAndBasicSelect(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Entity(Scope = "equeryubs")]
        public class Dict1
        {
            [AutoId]
            public int ID { get; set; }
            [EntityProperty(Sorted = true, Size = 32)]
            public string Name { get; set; }
            [EntityProperty(Sorted = true, Size = 32, DefaultValue = 512)]
            public int Value { get; set; }
        }

        [Entity(Scope = "equeryubs")]
        public class Dict2
        {
            [PrimaryKey]
            public int ID { get; set; }
            [EntityProperty(Sorted = true, Size = 32)]
            public string Name { get; set; }
        }

        [Entity(Scope = "equeryubs")]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty(Sorted = true, Size = 32, Nullable = true)]
            public string Name { get; set; }

            [ForeignKey(Nullable = true)]
            public Dict1 Dict { get; set; }

            [EntityProperty(Sorted = true)]
            public int? Value { get; set; }
        }

        private static List<Dict1> FillDict1(SqlDbConnection connection, int count)
        {
            var dict1 = new List<Dict1>();
            using (var tr = connection.BeginTransaction())
            {
                using (var query = connection.GetInsertEntityQuery<Dict1>())
                {
                    for (int i = 0; i < count; i++)
                    {
                        var d = new Dict1()
                        {
                            Name = $"Dict {i + 1:D2}",
                            Value = i,
                        };
                        query.Execute(d);
                        dict1.Add(d);
                    }
                }
                tr.Commit();
            }
            return dict1;
        }

        private static List<Dict2> FillDict2(SqlDbConnection connection, int count)
        {
            var dict2 = new List<Dict2>();
            using (var tr = connection.BeginTransaction())
            {
                using (var query = connection.GetInsertEntityQuery<Dict2>())
                {
                    for (int i = 0; i < count; i++)
                    {
                        var d = new Dict2()
                        {
                            ID = i + 1,
                            Name = $"Dict {i + 1:D2}"
                        };
                        query.Execute(d);
                        dict2.Add(d);
                    }
                }
                tr.Commit();
            }
            return dict2;
        }

        private static List<Entity> FillEntity(SqlDbConnection connection, int count)
        {
            List<Dict1> dict;
            using (var query = connection.GetSelectEntitiesQuery<Dict1>())
                dict = query.ReadAll<List<Dict1>, Dict1>();

            List<Entity> entities = new List<Entity>();
            using (var tr = connection.BeginTransaction())
            {
                using (var query = connection.GetInsertEntityQuery<Entity>())
                {
                    for (int i = 0; i < count; i++)
                    {
                        var d = new Entity()
                        {
                            Name = $"Entity {i + 1:D2}",
                            Value = i + 1,
                            Dict = dict[i % dict.Count]
                        };
                        query.Execute(d);
                        entities.Add(d);
                    }
                }
                tr.Commit();
            }
            return entities;
        }

        private static void Clear<T>(SqlDbConnection connection)
        {
            using (var query = connection.GetMultiDeleteEntityQuery<T>())
                query.Execute();
        }

        private static void ClearAll(SqlDbConnection connection)
        {
            Clear<Entity>(connection);
            Clear<Dict1>(connection);
            Clear<Dict2>(connection);
        }

        public class Fixture : ConnectionFixtureBase
        {
            public bool DeleteOnDispose { get; } = false;

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
                using (var query = connection.GetCreateEntityQuery<Dict1>())
                    query.Execute();
                using (var query = connection.GetCreateEntityQuery<Dict2>())
                    query.Execute();
                using (var query = connection.GetCreateEntityQuery<Entity>())
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

                using (var query = connection.GetDropEntityQuery<Dict1>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Dict2>())
                    query.Execute();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Insert_Autoincrement(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var d = new Dict1() { Name = "test", Value = 123 };

            using (var query = connection.GetInsertEntityQuery<Dict1>())
                query.Execute(d);

            d.ID.Should().BeGreaterThan(0);

            using (var query = connection.GetSelectOneEntityQuery<Dict1>(d.ID))
            {
                var r = query.ReadOne<Dict1>();
                r.Should().NotBeNull();
                r.Name.Should().Be("test");
                r.Value.Should().Be(123);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Insert_NoAutoincrement(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var d = new Dict2() { ID = 123, Name = "test" };

            using (var query = connection.GetInsertEntityQuery<Dict2>())
                query.Execute(d);

            using (var query = connection.GetSelectOneEntityQuery<Dict2>(d.ID))
            {
                var r = query.ReadOne<Dict2>();
                r.Should().NotBeNull();
                r.ID.Should().Be(123);
                r.Name.Should().Be("test");
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Insert_IgnoreAutoincrement(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var d = new Dict1() { ID = 15, Name = "test", Value = 123 };

            using (var query = connection.GetInsertEntityQuery<Dict1>(true))
                query.Execute(d);

            d.ID.Should().Be(15);

            using (var query = connection.GetSelectOneEntityQuery<Dict1>(d.ID))
            {
                var r = query.ReadOne<Dict1>();
                r.Should().NotBeNull();
                r.Name.Should().Be("test");
                r.Value.Should().Be(123);
            }

            if (connection is OracleDbConnection oracleConnection)
                oracleConnection.UpdateSequence<Dict1>();

            d = new Dict1() { Name = "test 1", Value = 456 };

            using (var query = connection.GetInsertEntityQuery<Dict1>())
                query.Execute(d);

            d.ID.Should().BeGreaterThan(15);

            using (var query = connection.GetSelectOneEntityQuery<Dict1>(d.ID))
            {
                var r = query.ReadOne<Dict1>();
                r.Should().NotBeNull();
                r.Name.Should().Be("test 1");
                r.Value.Should().Be(456);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void InsertSelect_Autoincrement(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var select = connection.GetSelectEntitiesQueryBase<Dict2>())
            {
                select.AddToResultset(nameof(Dict2.Name), "name");
                select.AddExpressionToResultset(
                    select.GetReference(nameof(Dict2.ID)).Alias + " * 2",
                    DbType.Int32, "value");

                if (connection.ConnectionType != UniversalSqlDbFactory.ORACLE)
                    select.AddOrderBy(nameof(Dict2.Name), SortDir.Desc);

                using (var insert = connection.GetInsertSelectEntityQuery<Dict1>(select))
                    insert.Execute();
            }

            using (var select = connection.GetSelectEntitiesQuery<Dict1>())
            {
                select.AddOrderBy(nameof(Dict1.ID));
                var dict1 = select.ReadAll<Dict1>();

                dict1.Should().HaveCount(10);
                if (connection.ConnectionType != UniversalSqlDbFactory.ORACLE)
                    dict1.Should().BeInDescendingOrder(s => s.Name);

                for (int i = 0; i < dict1.Count; i++)
                {
                    dict1[i].Name.Should().Match(n => dict2.Any(d => d.Name == n));
                    dict1[i].Value.Should().Be(dict2.First(d => d.Name == dict1[i].Name).ID * 2);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void InsertSelect_NoAutoincrement(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var select = connection.GetSelectEntitiesQueryBase<Dict2>())
            {
                select.AddExpressionToResultset(
                    select.GetReference(nameof(Dict2.ID)).Alias + " + 100",
                    DbType.Int32, "id");
                select.AddToResultset(nameof(Dict2.Name), "name");
                select.AddOrderBy(nameof(Dict2.Name), SortDir.Desc);

                using (var insert = connection.GetInsertSelectEntityQuery<Dict2>(select))
                    insert.Execute();
            }

            using (var select = connection.GetSelectEntitiesQuery<Dict2>())
            {
                var dict2a = select.ReadAll<Dict2>();

                dict2a.Should().HaveCount(20);

                for (int i = 0; i < dict2a.Count; i++)
                {
                    if (dict2a[i].ID < 100)
                        dict2.Should().HaveElementMatching(e => e.ID == dict2a[i].ID && e.Name == dict2a[i].Name);
                    else
                        dict2.Should().HaveElementMatching(e => e.ID == dict2a[i].ID - 100 && e.Name == dict2a[i].Name);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void InsertSelect_Autoincrement_OnlyProperties_DefaultValue(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var select = connection.GetSelectEntitiesQueryBase<Dict2>())
            {
                select.AddToResultset(nameof(Dict2.Name), "name");
                if (connection.ConnectionType != UniversalSqlDbFactory.ORACLE)
                    select.AddOrderBy(nameof(Dict2.Name), SortDir.Desc);

                using (var insert = connection.GetInsertSelectEntityQuery<Dict1>(select,
                    includeOnlyProperties: new string[] { nameof(Dict1.Name) }))
                    insert.Execute();
            }

            using (var select = connection.GetSelectEntitiesQuery<Dict1>())
            {
                select.AddOrderBy(nameof(Dict1.ID));
                var dict1 = select.ReadAll<Dict1>();

                dict1.Should().HaveCount(10);
                if (connection.ConnectionType != UniversalSqlDbFactory.ORACLE)
                    dict1.Should().BeInDescendingOrder(s => s.Name);

                for (int i = 0; i < dict1.Count; i++)
                {
                    dict1[i].Name.Should().Match(n => dict2.Any(d => d.Name == n));
                    dict1[i].Value.Should().Be(512);
                }
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void InsertSelect_IgnoreAutoincrement(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var select = connection.GetSelectEntitiesQueryBase<Dict2>())
            {
                select.AddToResultset(nameof(Dict2.ID), "id");
                select.AddToResultset(nameof(Dict2.Name), "name");
                select.AddToResultset(nameof(Dict2.ID), "value");
                select.AddOrderBy(nameof(Dict2.Name), SortDir.Asc);

                using (var insert = connection.GetInsertSelectEntityQuery<Dict1>(select, ignoreAutoIncrement: true))
                    insert.Execute();
            }

            EntityCollection<Dict1> dict1 = null;
            using (var select = connection.GetSelectEntitiesQuery<Dict1>())
            {
                select.AddOrderBy(nameof(Dict1.ID));
                dict1 = select.ReadAll<Dict1>();

                dict1.Should().HaveCount(10);
                dict1.Should().BeInAscendingOrder(s => s.Name);

                for (int i = 0; i < dict1.Count; i++)
                {
                    dict1[i].Name.Should().Match(n => dict2.Any(d => d.Name == n));
                    dict1[i].Value.Should().Be(dict1[i].ID);
                }
            }

            if (connection is OracleDbConnection oracleConnection)
                oracleConnection.UpdateSequence<Dict1>();

            var d = new Dict1() { Name = "test 1", Value = 456 };

            using (var query = connection.GetInsertEntityQuery<Dict1>())
                query.Execute(d);

            d.ID.Should().BeGreaterThan(dict1.Max(d => d.ID));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_ByOneEntity(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var query = connection.GetUpdateEntityQuery<Dict2>())
            {
                for (int i = 0; i < dict2.Count; i++)
                {
                    dict2[i].Name += " new";
                    query.Execute(dict2[i]);
                }
            }

            List<Dict2> dict2a;

            using (var query = connection.GetSelectEntitiesQuery<Dict2>())
                dict2a = query.ReadAll<List<Dict2>, Dict2>();

            dict2a.Should().HaveCount(10);
            for (int i = 0; i < dict2a.Count; i++)
                dict2a[i].Should().Match<Dict2>(d => dict2.Any(d1 => d1.ID == d.ID && d1.Name == d.Name));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_Multiple_ByValue(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var query = connection.GetMultiUpdateEntityQuery<Dict2>())
            {
                query.AddUpdateColumn(nameof(Dict2.Name), "new");
                query.Where.Property(nameof(Dict2.ID)).Ls(5);
                query.Execute();
            }

            List<Dict2> dict2a;

            using (var query = connection.GetSelectEntitiesQuery<Dict2>())
                dict2a = query.ReadAll<List<Dict2>, Dict2>();

            dict2a.Should().HaveCount(10);
            for (int i = 0; i < dict2a.Count; i++)
            {
                if (dict2a[i].ID >= 5)
                    dict2a[i].Should().Match<Dict2>(d => dict2.Any(d1 => d1.ID == d.ID && d1.Name == d.Name));
                else
                    dict2a[i].Should().Match<Dict2>(d => dict2.Any(d1 => d1.ID == d.ID && "new" == d.Name));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_Multiple_ByExpression(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var query = connection.GetMultiUpdateEntityQuery<Dict2>())
            {
                var ls = query.Query.Connection.GetLanguageSpecifics();
                query.AddUpdateColumnByExpression(nameof(Dict2.Name),
                    ls.GetSqlFunction(SqlFunctionId.Concat, new[] {
                        query.GetReference(nameof(Dict2.Name)).Alias,
                        ls.ParameterInQueryPrefix + "n"}));
                query.BindParam("n", " new");
                query.Where.Property(nameof(Dict2.ID)).Ls(5);
                query.Execute();
            }

            List<Dict2> dict2a;

            using (var query = connection.GetSelectEntitiesQuery<Dict2>())
                dict2a = query.ReadAll<List<Dict2>, Dict2>();

            dict2a.Should().HaveCount(10);
            for (int i = 0; i < dict2a.Count; i++)
            {
                if (dict2a[i].ID >= 5)
                    dict2a[i].Should().Match<Dict2>(d => dict2.Any(d1 => d1.ID == d.ID && d1.Name == d.Name));
                else
                    dict2a[i].Should().Match<Dict2>(d => dict2.Any(d1 => d1.ID == d.ID && d1.Name + " new" == d.Name));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Delete_ByOneEntity(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var query = connection.GetDeleteEntityQuery<Dict2>())
            {
                for (int i = 0; i < dict2.Count; i++)
                {
                    if (dict2[i].ID < 5)
                        query.Execute(dict2[i]);
                }
            }

            List<Dict2> dict2a;

            using (var query = connection.GetSelectEntitiesQuery<Dict2>())
                dict2a = query.ReadAll<List<Dict2>, Dict2>();

            dict2a.Should().HaveCount(dict2.Count(d => d.ID >= 5));
            dict2a.Should().HaveAllElementsMatching(d => d.ID >= 5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Delete_Multiple(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict2 = FillDict2(connection, 10);
            using (var query = connection.GetMultiDeleteEntityQuery<Dict2>())
            {
                query.Where.Property(nameof(Dict2.ID)).Ls(5);
                query.Execute();
            }

            List<Dict2> dict2a;

            using (var query = connection.GetSelectEntitiesQuery<Dict2>())
                dict2a = query.ReadAll<List<Dict2>, Dict2>();

            dict2a.Should().HaveCount(dict2.Count(d => d.ID >= 5));
            dict2a.Should().HaveAllElementsMatching(d => d.ID >= 5);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resulset_AllByDefault(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);

                entities1.Should().HaveAllElementsMatching(e1 =>
                    entities.Any(e => e.ID == e1.ID &&
                                      e.Name == e1.Name &&
                                      e.Value == e1.Value &&
                                      e.Dict.ID == e1.Dict.ID &&
                                      e.Dict.Name == e1.Dict.Name &&
                                      e.Dict.Value == e1.Dict.Value));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resulset_ExcludeOrdinaryField(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);

            using (var query = connection.GetSelectEntitiesQuery<Entity>(new[] { new SelectEntityQueryFilter() { EntityType = typeof(Dict1), Property = nameof(Dict1.Value) } }))
            {
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);

                entities1.Should().HaveAllElementsMatching(e1 =>
                    entities.Any(e => e.ID == e1.ID &&
                                      e.Name == e1.Name &&
                                      e.Value == e1.Value &&
                                      e.Dict.ID == e1.Dict.ID &&
                                      e.Dict.Name == e1.Dict.Name &&
                                      0 == e1.Dict.Value));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resulset_ExcludeReference(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);

            using (var query = connection.GetSelectEntitiesQuery<Entity>(new[] { new SelectEntityQueryFilter() { EntityType = typeof(Entity), Property = nameof(Entity.Dict) } }))
            {
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);

                entities1.Should().HaveAllElementsMatching(e1 =>
                    entities.Any(e => e.ID == e1.ID &&
                                      e.Name == e1.Name &&
                                      e.Value == e1.Value &&
                                      null == e1.Dict));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resulset_ExcludeReference_UseInCondition(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);

            using (var query = connection.GetSelectEntitiesQuery<Entity>(new[] { new SelectEntityQueryFilter() { EntityType = typeof(Entity), Property = nameof(Entity.Dict) } }))
            {
                query.Where.PropertyOf<Dict1>(nameof(Dict1.Name)).Like("%5%");
                var entities1 = query.ReadAll<Entity>();
                entities1.Count.Should().BeGreaterThan(0);
                entities1.Should().HaveAllElementsMatching(e1 =>
                    entities.Any(e => e.ID == e1.ID && e.Dict.Name.Contains('5')));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectCount_Simple(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            FillEntity(connection, 50);

            using (var query = connection.GetSelectEntitiesCountQuery<Entity>())
                query.RowCount.Should().Be(50);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SelectCount_WithCondition(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);

            using (var query = connection.GetSelectEntitiesCountQuery<Entity>())
            {
                query.Where.Property(nameof(Entity.Name)).Like("%5%");
                query.RowCount.Should()
                    .BeGreaterThan(0)
                    .And
                    .Be(entities.Count(e => e.Name.Contains('5')));
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resultset_Size_Limit(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.AddOrderBy(nameof(Entity.ID));
                query.Limit = 5;
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(5);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resultset_Size_Skip(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.AddOrderBy(nameof(Entity.ID));
                query.Skip = 10;
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count - 10);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i + 10].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Resultset_Size_SkipLimit(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.AddOrderBy(nameof(Entity.ID));
                query.Skip = 10;
                query.Limit = 15;
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(15);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i + 10].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_FirstEntity(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            var mid = entities.Max(d => d.ID);
            entities = entities.Where(b => b.ID > mid - 20).ToList();
            entities.Count.Should().BeGreaterThan(0);

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.Where.Property(nameof(Entity.ID)).Gt(mid - 20);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_SecondEntity_ByType(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict = FillDict1(connection, 10);
            var mid = dict.Max(d => d.ID);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            entities = entities.Where(b => b.Dict.ID < mid - 5).ToList();
            entities.Count.Should().BeGreaterThan(0);

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.Where.PropertyOf<Dict1>(nameof(Dict1.ID)).Ls(mid - 5);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_SecondEntity_ByPath(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict = FillDict1(connection, 10);
            var mid = dict.Max(d => d.ID);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            entities = entities.Where(b => b.Dict.ID <= mid - 5).ToList();
            entities.Count.Should().BeGreaterThan(0);

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.Where.Property($"{nameof(Entity.Dict)}.{nameof(Dict1.ID)}").Le(mid - 5);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_In_Values(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            var id1 = entities[0].ID;
            var id2 = entities[5].ID;
            var id3 = entities[12].ID;
            entities = entities.Where(b => b.ID == id1 || b.ID == id2 || b.ID == id3).ToList();
            entities.Count.Should().BeGreaterThan(0);

            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                query.Where.Property(nameof(Entity.ID)).In().Values(id1, id2, id3);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_In_Select(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            entities = entities.Where(b => b.Dict.Name.Contains('5')).ToList();
            entities.Count.Should().BeGreaterThan(0);

            using (var subquery = connection.GetSelectEntitiesQueryBase<Dict1>())
            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                subquery.AddToResultset(nameof(Dict1.ID));
                subquery.Where.Property(nameof(Dict1.Name)).Like("%5%");

                query.Where.Property(nameof(Entity.Dict)).In().Query(subquery);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_In_ConnectedSelect(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            entities = entities.Where(b => b.ID == entities.Where(b1 => b1.Dict.ID == b.Dict.ID).Max(b => b.ID)).ToList();
            entities.Count.Should().BeGreaterThan(0);

            using (var subquery = connection.GetSelectEntitiesQueryBase<Entity>())
            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                var dictInQuery = query.GetReference(nameof(Entity.Dict));

                subquery.AddToResultset(AggFn.Max, nameof(Entity.ID));
                subquery.Where.Property(nameof(Entity.Dict)).Eq().Reference(dictInQuery);

                query.Where.Property(nameof(Entity.ID)).In().Query(subquery);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Where_Eq_ConnectedSelect(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            entities.Sort((a, b) => a.ID.CompareTo(b.ID));
            entities = entities.Where(b => b.ID == entities.Where(b1 => b1.Dict.ID == b.Dict.ID).Min(b => b.ID)).ToList();
            entities.Count.Should().BeGreaterThan(0);
            entities.Count.Should().BeLessOrEqualTo(10);

            using (var subquery = connection.GetSelectEntitiesQueryBase<Entity>())
            using (var query = connection.GetSelectEntitiesQuery<Entity>())
            {
                var dictInQuery = query.GetReference(nameof(Entity.Dict));

                subquery.AddToResultset(AggFn.Min, nameof(Entity.ID));
                subquery.Where.Property(nameof(Entity.Dict)).Eq().Reference(dictInQuery);

                query.Where.Property(nameof(Entity.ID)).Eq().Query(subquery);
                query.AddOrderBy(nameof(Entity.ID));
                var entities1 = query.ReadAll<Entity>();
                entities1.Should().HaveCount(entities.Count);
                for (int i = 0; i < entities1.Count; i++)
                    entities1[i].ID.Should().Be(entities[i].ID);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Select_Having(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            using var delayed = new DelayedAction(() => ClearAll(connection));

            var dict = FillDict1(connection, 10);
            var entities = FillEntity(connection, 50);
            var mid = entities.Max(e => e.ID);

            using (var query = connection.GetSelectEntitiesQueryBase<Dict1>())
            {
                query.AddEntity<Entity>();

                query.AddToResultset(nameof(Dict1.ID), "id");
                query.AddToResultset(nameof(Dict1.Name), "name");
                query.AddToResultset(AggFn.Max, typeof(Entity), nameof(Entity.ID), "ec");

                query.AddGroupBy(nameof(Dict1.ID));

                query.Having.PropertyOf(nameof(Entity.ID), typeof(Entity)).Max()
                    .Gt()
                    .Value(mid - 5);

                query.Execute();
                var r = query.ReadAllDynamic();
                r.Count.Should().BeGreaterThan(0);
                for (int i = 0; i < r.Count; i++)
                {
                    int id = (int)r[i].id;
                    string name = (string)r[i].name;
                    int ec = (int)r[i].ec;

                    name.Should().Be(dict.First(e => e.ID == id).Name);
                    ec.Should().BeGreaterThan(mid - 5);
                    ec.Should().Be(entities.Where(e => e.Dict.ID == id).Max(e => e.ID));
                }
            }
        }
    }
}


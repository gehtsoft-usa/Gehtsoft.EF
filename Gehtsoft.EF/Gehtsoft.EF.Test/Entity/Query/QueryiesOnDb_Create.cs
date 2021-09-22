using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using LiquidTestReports.Core.Filters;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class QueryiesOnDb_Create : IClassFixture<QueryiesOnDb_Create.Fixture>
    {
        private const string mFlags = "";
        public static IEnumerable<object[]> ConnectionNames(string flags = "") => SqlConnectionSources.SqlConnectionNames(flags, mFlags);

        [Entity(Scope = "createntity0", Table = "ce_table0", Metadata = typeof(Entity0Metadata))]
        public class Entity0
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Field = "n", Size = 32)]
            public string Name { get; set; }

            [EntityProperty(Sorted = true, Size = 32)]
            public string Value { get; set; }
        }

        public class Entity0Metadata : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes
            {
                get
                {
                    var ix = new CompositeIndex("i1")
                    {
                        nameof(Entity0.Name)
                    };
                    yield return ix;
                }
            }
        }

        //advanced test
        //version 1
        //e0 (id, name, note) -> d1 -> d0_1
        //version 2
        //e0 (id, name, -note, +comment) -> d1 -> d0_2

        [Entity(Scope = "createntity1", Table = "ce_dict0")]
        public class Dict0_V1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "createntity1", Table = "ce_dict1")]
        public class Dict1_V1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }

            [ForeignKey]
            public Dict0_V1 Dict { get; set; }
        }

        [Entity(Scope = "createntity1", Table = "ce_table1")]
        public class Entity0_V1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }

            [EntityProperty]
            public string Note { get; set; }

            [ForeignKey]
            public Dict1_V1 Dict { get; set; }
        }

        [ObsoleteEntity(Scope = "createntity2", Table = "ce_dict0")]
        public class Dict0_V2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "createntity2", Table = "ce_dict2")]
        public class Dict2_V2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "createntity2", Table = "ce_dict1")]
        public class Dict1_V2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }

            [ObsoleteEntityProperty(ForeignKey = true)]
            [Obsolete("Use Dict1 instead")]
            public Dict0_V2 Dict { get; set; }

            [ForeignKey]
            public Dict2_V2 Dict1 { get; set; }
        }

        [Entity(Scope = "createntity2", Table = "ce_table1")]
        public class Entity0_V2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public string Name { get; set; }

            [ObsoleteEntityProperty]
            public string Note { get; set; }

            [EntityProperty]
            public string Note1 { get; set; }

            [ForeignKey]
            public Dict1_V2 Dict { get; set; }
        }

        public class View0Metadata<T> : IViewCreationMetadata
        {
            public SelectQueryBuilder GetSelectQuery(SqlDbConnection connection)
            {
                var td = AllEntities.Get<T>().TableDescriptor;
                var builder = connection.GetSelectQueryBuilder(td);
                builder.AddToResultset(td[nameof(Dict0_V1.Name)], nameof(Dict0_V1.Name));
                return builder;
            }
        }

        [Entity(Scope = "createntity1", Table = "ce_view", View = true, Metadata = typeof(View0Metadata<Dict0_V1>))]
        public class View0_V1
        {
            [EntityProperty]
            public string Name { get; set; }
        }

        [Entity(Scope = "createntity2", Table = "ce_view", View = true, Metadata = typeof(View0Metadata<Dict2_V2>))]
        public class View0_V2
        {
            [EntityProperty]
            public string Name { get; set; }
        }

        public class Fixture : SqlConnectionFixtureBase
        {
            public bool DeleteOnDispose { get; } = false;

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
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
                using (var query = connection.GetDropViewQuery<View0_V2>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Entity0>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Entity0_V2>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Dict1_V2>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Dict0_V2>())
                    query.Execute();

                using (var query = connection.GetDropEntityQuery<Dict2_V2>())
                    query.Execute();
            }
        }

        private readonly Fixture mFixture;

        public QueryiesOnDb_Create(Fixture fixture)
        {
            mFixture = fixture;
        }

        [TestOrder(1)]
        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateEntity_Implicitly(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetCreateEntityQuery<Entity0>())
                query.Execute();

            connection.DoesObjectExist("ce_table0", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_table0", "i1", "index").Should().BeTrue();
            connection.DoesObjectExist("ce_table0", "value", "index").Should().BeTrue();

            using (var query = connection.GetSelectEntitiesCountQuery<Entity0>())
                query.RowCount.Should().Be(0);
        }

        [TestOrder(2)]
        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void DropEntity_Implicitly(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                CreateEntity_Implicitly(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            connection.DoesObjectExist("ce_table0", null, "table").Should().BeTrue();

            using (var query = connection.GetDropEntityQuery<Entity0>())
                query.Execute();

            connection.DoesObjectExist("ce_table0", null, "table").Should().BeFalse();
        }

        [TestOrder(20)]
        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void EntityCreatorFlow_Step1(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            var creator = new CreateEntityController(this.GetType().Assembly, "createntity1");
            creator.UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            connection.DoesObjectExist("ce_dict0", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_dict0", "id", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_dict0", "name", "column").Should().BeTrue();

            connection.DoesObjectExist("ce_dict1", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_dict1", "id", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_dict1", "name", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_dict1", "dict", "column").Should().BeTrue();

            connection.DoesObjectExist("ce_table1", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "id", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "name", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "note", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "dict", "column").Should().BeTrue();

            connection.DoesObjectExist("ce_view", null, "view").Should().BeTrue();
        }

        [TestOrder(21)]
        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void EntityCreatorFlow_Step2(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                EntityCreatorFlow_Step1(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var creator = new CreateEntityController(this.GetType().Assembly, "createntity2");
            creator.UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            var canDrop = connection.GetLanguageSpecifics().DropColumnSupported;

            if (canDrop)
                connection.DoesObjectExist("ce_dict0", null, "table").Should().BeFalse();
            else
                connection.DoesObjectExist("ce_dict0", null, "table").Should().BeTrue();

            connection.DoesObjectExist("ce_dict2", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_dict2", "id", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_dict2", "name", "column").Should().BeTrue();

            connection.DoesObjectExist("ce_dict1", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_dict1", "id", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_dict1", "name", "column").Should().BeTrue();
            if (canDrop)
                connection.DoesObjectExist("ce_dict1", "dict", "column").Should().BeFalse();
            else
                connection.DoesObjectExist("ce_dict1", "dict", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_dict1", "dict1", "column").Should().BeTrue();

            connection.DoesObjectExist("ce_table1", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "id", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "name", "column").Should().BeTrue();
            if (canDrop)
                connection.DoesObjectExist("ce_table1", "note", "column").Should().BeFalse();
            else
                connection.DoesObjectExist("ce_table1", "note", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "note1", "column").Should().BeTrue();
            connection.DoesObjectExist("ce_table1", "dict", "column").Should().BeTrue();

            connection.DoesObjectExist("ce_view", null, "view").Should().BeTrue();
        }
    }
}
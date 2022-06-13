using System;
using System.Collections.Generic;
using System.Reflection;
using Castle.Core.Internal;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Moq;
using Xunit;

#pragma warning disable S1125 // Boolean literals should not be redundant
#pragma warning disable S1172 // Unused method parameters should be removed

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class EntityCreateControllerUnit
    {
        private static bool Dict1CreateCalled { get; set; } = false;
        private static void OnDict1Create(SqlDbConnection _) => Dict1CreateCalled = true;

        private static bool Dict1DropCalled { get; set; } = false;
        private static void OnDict1Drop(SqlDbConnection _) => Dict1DropCalled = true;

        [OnEntityCreate(typeof(EntityCreateControllerUnit), nameof(OnDict1Create))]
        [OnEntityDrop(typeof(EntityCreateControllerUnit), nameof(OnDict1Drop))]
        [Entity(Scope = "creator_order_1")]
        public class TestOrder_Dict1
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public int Name { get; set; }
        }

        [ObsoleteEntity(Scope = "creator_order_1")]
        public class TestOrder_Dict3
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public int Name { get; set; }
        }

        [ObsoleteEntity(Scope = "creator_order_1")]
        public class TestOrder_Dict4
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public int Name { get; set; }
        }

        [Entity(Scope = "creator_order_1")]
        public class TestOrder_Dict2
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public int Name { get; set; }

            [ForeignKey]
            public TestOrder_Dict1 R { get; set; }

            [ObsoleteEntityProperty(ForeignKey = true)]
            public TestOrder_Dict3 R1 { get; set; }

            [ObsoleteEntityProperty]
            public string OldNote { get; set; }

            [EntityProperty]
            public string NewNote { get; set; }
        }

        [Entity(Scope = "creator_order_1")]
        public class TestOrder_Entity
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public int Name { get; set; }

            [ForeignKey]
            public TestOrder_Dict2 R { get; set; }
        }

        [Entity(Scope = "creator_order_1", View = true, Metadata = typeof(IViewCreationMetadata))]
        public class TestOrder_View
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public int Name { get; set; }
        }

        [Fact]
        public void Create()
        {
            using var connection = new DummySqlConnection();
            var action = new Mock<CreateEntityController.ICreateEntityControllerAction>(MockBehavior.Strict);
            var sequence = new MockSequence();

            var assemblies = new List<Assembly>() { this.GetType().Assembly };

            EntityFinder.EntityTypeInfo[] es = EntityFinder.FindEntities(assemblies, "creator_order_1", false);

            var e1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Entity));
            var d1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict1));
            var d2 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict2));
            var v1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_View));

            action.Setup(x => x.FindEntities(
                It.Is<IEnumerable<Assembly>>(e => e == assemblies),
                It.Is<string>(s => s == "scope"),
                It.Is<bool>(b => b == false)))
                .Returns(new EntityFinder.EntityTypeInfo[] { e1, d2, v1, d1 });

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d2))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == e1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            var controller = new CreateEntityController(assemblies, "scope")
            {
                ActionController = action.Object
            };

            List<string> actions = new List<string>();

            controller.OnAction += (s, args) =>
            {
                args.EventAction.Should().Be(CreateEntityControllerEventArgs.Action.Create);
                actions.Add(args.Table);
            };

            Dict1CreateCalled = false;
            controller.CreateTables(connection);

            action.Verify();

            Dict1CreateCalled.Should().BeTrue();

            actions.Should().HaveCount(4);
            actions.Should().BeEquivalentTo(new string[] { d1.Table, d2.Table, e1.Table, v1.Table });
        }

        [Fact]
        public void Drop()
        {
            using var connection = new DummySqlConnection();
            var action = new Mock<CreateEntityController.ICreateEntityControllerAction>(MockBehavior.Strict);
            var sequence = new MockSequence();

            var assemblies = new List<Assembly>() { this.GetType().Assembly };

            var es = EntityFinder.FindEntities(assemblies, "creator_order_1", true);

            var e1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Entity));
            var d1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict1));
            var d2 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict2));
            var d3 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict3));
            var v1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_View));

            action.Setup(x => x.FindEntities(
                It.Is<IEnumerable<Assembly>>(e => e == assemblies),
                It.Is<string>(s => s == "scope"),
                It.Is<bool>(b => b == true)))
                .Returns(new EntityFinder.EntityTypeInfo[] { d3, e1, d2, v1, d1 });

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1)))
                .Verifiable();

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == e1)))
                .Verifiable();

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d2)))
                .Verifiable();

            EntityFinder.EntityTypeInfo alreadyDropped = null;

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d3 || e == d1)))
                .Callback<SqlDbConnection, EntityFinder.EntityTypeInfo>((_, e) => alreadyDropped = e)
                .Verifiable();

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e != alreadyDropped && (e == d3 || e == d1))))
                .Verifiable();

            var controller = new CreateEntityController(assemblies, "scope")
            {
                ActionController = action.Object
            };

            List<string> actions = new List<string>();

            controller.OnAction += (s, args) =>
            {
                args.EventAction.Should().Be(CreateEntityControllerEventArgs.Action.Drop);
                actions.Add(args.Table);
            };

            Dict1DropCalled = false;
            controller.DropTables(connection);

            action.Verify();

            Dict1DropCalled.Should().BeTrue();

            actions.Should().HaveCount(5);
            actions.Should().BeEquivalentTo(new string[] { v1.Table, e1.Table, d2.Table, d3.Table, d1.Table });
        }

        [Fact]
        public void Update_FirstRun()
        {
            using var connection = new DummySqlConnection();

            var action = new Mock<CreateEntityController.ICreateEntityControllerAction>(MockBehavior.Strict);
            var sequence = new MockSequence();

            var assemblies = new List<Assembly>() { this.GetType().Assembly };

            var es = EntityFinder.FindEntities(assemblies, "creator_order_1", true);

            var e1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Entity));
            var d1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict1));
            var d2 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict2));
            var d3 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict3));
            var d4 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict4));
            var v1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_View));

            connection.SetSchema(Array.Empty<TableDescriptor>());

            action.Setup(x => x.FindEntities(
                It.Is<IEnumerable<Assembly>>(e => e == assemblies),
                It.Is<string>(s => s == "scope"),
                It.Is<bool>(b => b == true)))
                .Returns(new EntityFinder.EntityTypeInfo[] { e1, d2, v1, d1, d3, d4 });

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d2))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == e1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            var controller = new CreateEntityController(assemblies, "scope")
            {
                ActionController = action.Object
            };

            List<string> actions = new List<string>();

            controller.OnAction += (s, args) =>
            {
                args.EventAction.Should().Be(CreateEntityControllerEventArgs.Action.Create);
                actions.Add(args.Table);
            };

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            action.Verify();

            Dict1CreateCalled.Should().BeTrue();

            actions.Should().HaveCount(4);
            actions.Should().BeEquivalentTo(new string[] { d1.Table, d2.Table, e1.Table, v1.Table });
        }

        [Fact]
        public void Update_CreateNew()
        {
            using var connection = new DummySqlConnection();

            var action = new Mock<CreateEntityController.ICreateEntityControllerAction>(MockBehavior.Strict);
            var sequence = new MockSequence();

            var assemblies = new List<Assembly>() { this.GetType().Assembly };

            var es = EntityFinder.FindEntities(assemblies, "creator_order_1", true);

            var e1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Entity));
            var d1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict1));
            var d2 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict2));
            var d3 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict3));
            var d4 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict4));
            var v1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_View));

            var td1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict1)
            };
            td1.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            td1.Add(new TableDescriptor.ColumnInfo() { Name = "name" });

            var td2 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict2)
            };
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "name" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "r" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "newnote" });

            connection.SetSchema(new TableDescriptor[] { td1, td2 });

            action.Setup(x => x.FindEntities(
                It.Is<IEnumerable<Assembly>>(e => e == assemblies),
                It.Is<string>(s => s == "scope"),
                It.Is<bool>(b => b == true)))
                .Returns(new EntityFinder.EntityTypeInfo[] { e1, d2, v1, d1, d3, d4 });

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == e1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            var controller = new CreateEntityController(assemblies, "scope")
            {
                ActionController = action.Object
            };

            List<string> actions = new List<string>();

            controller.OnAction += (s, args) =>
            {
                args.EventAction.Should().Be(CreateEntityControllerEventArgs.Action.Create);
                actions.Add(args.Table);
            };

            Dict1CreateCalled = false;
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            action.Verify();

            Dict1CreateCalled.Should().BeFalse();

            actions.Should().HaveCount(2);
            actions.Should().BeEquivalentTo(new string[] { e1.Table, v1.Table });
        }

        [Fact]
        public void Update_SecondRun_CanDropColumns()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.DropColumnSupportedSpec = true;

            var action = new Mock<CreateEntityController.ICreateEntityControllerAction>(MockBehavior.Strict);
            var sequence = new MockSequence();

            var assemblies = new List<Assembly>() { this.GetType().Assembly };

            var es = EntityFinder.FindEntities(assemblies, "creator_order_1", true);

            var e1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Entity));
            var d1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict1));
            var d2 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict2));
            var d3 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict3));
            var d4 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict4));
            var v1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_View));

            var td1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict1)
            };
            td1.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            td1.Add(new TableDescriptor.ColumnInfo() { Name = "name" });

            var td2 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict2)
            };
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "name" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "r" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "r1" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "oldnote" });

            var td3 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict3)
            };

            var td4 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict4)
            };

            var te1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Entity)
            };
            te1.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            te1.Add(new TableDescriptor.ColumnInfo() { Name = "name" });
            te1.Add(new TableDescriptor.ColumnInfo() { Name = "r" });

            var tv1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_View)
            };

            connection.SetSchema(new TableDescriptor[] { td1, td2, td3, te1, tv1, td4 });

            action.Setup(x => x.FindEntities(
                It.Is<IEnumerable<Assembly>>(e => e == assemblies),
                It.Is<string>(s => s == "scope"),
                It.Is<bool>(b => b == true)))
                .Returns(new EntityFinder.EntityTypeInfo[] { e1, d2, v1, d1, d3, d4 });

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d4))).Verifiable();

            action.InSequence(sequence).Setup(x => x.DropColumns(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d2),
                It.Is<TableDescriptor>(td => td.Name == td2.Name),
                It.Is<TableDescriptor.ColumnInfo[]>(ci => ci.Length == 2 &&
                                                          ci[0].Name == "r1" &&
                                                          ci[1].Name == "oldnote")))
                    .Verifiable();

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d3))).Verifiable();

            action.InSequence(sequence).Setup(x => x.AddColumns(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d2),
                It.Is<TableDescriptor>(td => td.Name == td2.Name),
                It.Is<TableDescriptor.ColumnInfo[]>(ci => ci.Length == 1 &&
                                                   ci[0].Name == "newnote")))
                    .Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            var controller = new CreateEntityController(assemblies, "scope")
            {
                ActionController = action.Object
            };

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            action.Verify();
        }

        [Fact]
        public void Update_SecondRun_CanNotDropColumns()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.DropColumnSupportedSpec = false;

            var action = new Mock<CreateEntityController.ICreateEntityControllerAction>(MockBehavior.Strict);
            var sequence = new MockSequence();

            var assemblies = new List<Assembly>() { this.GetType().Assembly };

            var es = EntityFinder.FindEntities(assemblies, "creator_order_1", true);

            var e1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Entity));
            var d1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict1));
            var d2 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict2));
            var d3 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict3));
            var d4 = Array.Find(es, e => e.EntityType == typeof(TestOrder_Dict4));
            var v1 = Array.Find(es, e => e.EntityType == typeof(TestOrder_View));

            var td1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict1)
            };
            td1.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            td1.Add(new TableDescriptor.ColumnInfo() { Name = "name" });

            var td2 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict2)
            };
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "name" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "r" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "r1" });
            td2.Add(new TableDescriptor.ColumnInfo() { Name = "oldnote" });

            var td3 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict3)
            };

            var td4 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Dict4)
            };

            var te1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_Entity)
            };
            te1.Add(new TableDescriptor.ColumnInfo() { Name = "id" });
            te1.Add(new TableDescriptor.ColumnInfo() { Name = "name" });
            te1.Add(new TableDescriptor.ColumnInfo() { Name = "r" });

            var tv1 = new TableDescriptor()
            {
                Name = nameof(TestOrder_View)
            };

            connection.SetSchema(new TableDescriptor[] { td1, td2, td3, te1, tv1, td4 });

            action.Setup(x => x.FindEntities(
                It.Is<IEnumerable<Assembly>>(e => e == assemblies),
                It.Is<string>(s => s == "scope"),
                It.Is<bool>(b => b == true)))
                .Returns(new EntityFinder.EntityTypeInfo[] { e1, d2, v1, d1, d3, d4 });

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            action.InSequence(sequence).Setup(x => x.Drop(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d4))).Verifiable();

            action.InSequence(sequence).Setup(x => x.AddColumns(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == d2),
                It.Is<TableDescriptor>(td => td.Name == td2.Name),
                It.Is<TableDescriptor.ColumnInfo[]>(ci => ci.Length == 1 &&
                                                   ci[0].Name == "newnote")))
                    .Verifiable();

            action.InSequence(sequence).Setup(x => x.Create(
                It.Is<SqlDbConnection>(c => c == connection),
                It.Is<EntityFinder.EntityTypeInfo>(e => e == v1))).Verifiable();

            var controller = new CreateEntityController(assemblies, "scope")
            {
                ActionController = action.Object
            };

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            action.Verify();
        }
    }
}


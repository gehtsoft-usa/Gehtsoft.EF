using System;
using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    // Reproduction tests for defect C5:
    // "SQLite 'table <name> already exists' on the second CreateEntities(true)
    //  / UpdateTables(..., Recreate) call".
    // See C5.md in the CoinAccountant repo for the full analysis.
    public class C5_RecreateTwiceSqliteTests
    {
        [Entity(Scope = "rc2", Table = "rc2_parent")]
        public class Parent
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }
        }

        [Entity(Scope = "rc2", Table = "rc2_child")]
        public class Child
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "parent", ForeignKey = true)]
            public Parent Parent { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 64)]
            public string Name { get; set; }
        }

        [Fact]
        public void Recreate_Twice_On_SameSqliteConnection_Succeeds()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();

            var controller = new CreateEntityController(typeof(Parent), "rc2");

            // First call: create from empty schema.
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);
            connection.Schema().Contains("rc2_parent").Should().BeTrue();
            connection.Schema().Contains("rc2_child").Should().BeTrue();

            // Seed a row so we can verify truncation on the second pass.
            using (var ins = connection.GetInsertEntityQuery<Parent>())
                ins.Execute(new Parent { Name = "p1" });

            // Second call: drop + create from existing schema.
            // BUG: today this throws
            //   SqliteException : 'table rc2_parent already exists'
            ((Action)(() =>
                controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate)))
                .Should().NotThrow();

            connection.Schema().Contains("rc2_parent").Should().BeTrue();
            connection.Schema().Contains("rc2_child").Should().BeTrue();

            // Table should be empty after Recreate.
            using var countQ = connection.GetSelectEntitiesCountQuery<Parent>();
            countQ.RowCount.Should().Be(0);
        }

        [Entity(Scope = "rc3", Table = "rc3_a")]
        public class A
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        [Entity(Scope = "rc3", Table = "rc3_b")]
        public class B
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "ref", ForeignKey = true)]
            public A Ref { get; set; }
        }

        [Entity(Scope = "rc3", Table = "rc3_c")]
        public class C
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "ref", ForeignKey = true)]
            public A Ref { get; set; }

            [EntityProperty(Field = "ref2", ForeignKey = true)]
            public B Ref2 { get; set; }
        }

        [Fact]
        public void Recreate_Twice_With_Deep_FK_Fan_Out_Succeeds()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = new CreateEntityController(typeof(A), "rc3");

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            ((Action)(() =>
                controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate)))
                .Should().NotThrow();

            connection.Schema().Contains("rc3_a").Should().BeTrue();
            connection.Schema().Contains("rc3_b").Should().BeTrue();
            connection.Schema().Contains("rc3_c").Should().BeTrue();
        }

        [Fact]
        public void Recreate_Twice_Emits_Drop_Before_Create_For_Each_Table()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = new CreateEntityController(typeof(Parent), "rc2");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            var order = new List<string>();
            controller.OnAction += (_, e) => order.Add($"{e.EventAction}:{e.Table}");

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            // Expect an interleaved Drop-then-Create for every table.
            // When the bug fires, we will see 'Create:rc2_parent' without a
            // preceding 'Drop:rc2_parent' — that's the smoking gun.
            foreach (var t in new[] { "rc2_parent", "rc2_child" })
            {
                var dropIdx = order.IndexOf($"Drop:{t}");
                var createIdx = order.IndexOf($"Create:{t}");
                dropIdx.Should().BeGreaterThanOrEqualTo(0, $"expected Drop:{t} to be emitted");
                createIdx.Should().BeGreaterThan(dropIdx, $"expected Drop before Create for {t}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

#pragma warning disable CS0618 // this suite deliberately exercises the obsolete CreateEntityController shim

namespace Gehtsoft.EF.Test.Entity.Query
{
    /// <summary>
    /// Covers the obsolete public <see cref="CreateEntityController"/> pass-through shim itself: it must be
    /// marked obsolete, and it must faithfully delegate to the internal introspection implementation
    /// (create the tables, forward the <c>OnAction</c> event). The rest of the suite deliberately uses
    /// <c>CreateEntityControllerInternal</c> directly; this is the one place the shim is exercised.
    /// </summary>
    public sealed class CreateEntityControllerObsoleteShimTest
    {
        private const string ShimScope = "obsolete_shim";

        [Entity(Scope = ShimScope, Table = "obsolete_shim_a")]
        public class ShimEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }
        }

        [Fact]
        public void CreateEntityController_IsMarkedObsolete()
        {
            typeof(CreateEntityController).GetCustomAttribute<ObsoleteAttribute>()
                .Should().NotBeNull("the old controller is deprecated in favour of CatalogEntityController");
        }

        [Fact]
        public void Shim_UpdateTables_DelegatesAndCreatesTable_AndForwardsEvents()
        {
            using var connection = SqliteDbConnectionFactory.CreateMemory();

#pragma warning disable CS0618 // intentionally exercising the obsolete shim
            var controller = new CreateEntityController(typeof(CreateEntityControllerObsoleteShimTest).Assembly, ShimScope);
            var raised = new List<string>();
            controller.OnAction += (sender, args) => raised.Add(args.Table);
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
#pragma warning restore CS0618

            // Delegated to the internal implementation: the table exists and the event forwarded.
            connection.DoesObjectExist("obsolete_shim_a", null, "table").Should().BeTrue();
            raised.Should().Contain("obsolete_shim_a");
        }
    }
}

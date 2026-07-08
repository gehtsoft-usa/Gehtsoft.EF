using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.TableManagement
{
    public class DynamicPropertiesUpdateTablesTest
    {
        // --- "gained" transition: same table, before has no props, after has props ---

        [Entity(Scope = "dp_gain_before", Table = "dp_gain")]
        public class GainBefore
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }
        }

        [Entity(Scope = "dp_gain_after", Table = "dp_gain")]
        [DynamicProperties]
        public class GainAfter : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        // --- "lost" transition: same table, before has props, after does not ---

        [Entity(Scope = "dp_lost_before", Table = "dp_lost")]
        [DynamicProperties]
        public class LostBefore : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dp_lost_after", Table = "dp_lost")]
        public class LostAfter
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }
        }

        [Entity(Scope = "dp_idem", Table = "dp_idem")]
        [DynamicProperties]
        public class IdemOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dp_fp", Table = "dp_fp")]
        public class FalsePositiveOwner
        {
            [AutoId]
            public int Id { get; set; }
        }

        [Fact]
        public void UpdateTables_GainedDynamicProperties_CreatesPropsTable()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();

            new CreateEntityController(typeof(GainBefore), "dp_gain_before")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            connection.DoesObjectExist("dp_gain", null, "table").Should().BeTrue();
            connection.DoesObjectExist("dp_gain_props", null, "table").Should().BeFalse();

            new CreateEntityController(typeof(GainAfter), "dp_gain_after")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            connection.DoesObjectExist("dp_gain", null, "table").Should().BeTrue();
            connection.DoesObjectExist("dp_gain_props", null, "table").Should().BeTrue();

            foreach (string index in new[] { "owner", "owner_name", "name_str", "name_int", "name_real" })
                connection.DoesObjectExist("dp_gain_props", index, "index").Should().BeTrue($"index dp_gain_props_{index}");
        }

        [Fact]
        public void UpdateTables_LostDynamicProperties_DropsOrphanPropsTable()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();

            new CreateEntityController(typeof(LostBefore), "dp_lost_before")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            connection.DoesObjectExist("dp_lost", null, "table").Should().BeTrue();
            connection.DoesObjectExist("dp_lost_props", null, "table").Should().BeTrue();

            new CreateEntityController(typeof(LostAfter), "dp_lost_after")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            connection.DoesObjectExist("dp_lost", null, "table").Should().BeTrue("owner table must remain");
            connection.DoesObjectExist("dp_lost_props", null, "table").Should().BeFalse("orphan props table must be dropped");
        }

        [Fact]
        public void UpdateTables_Idempotent_WhenPropsAlreadyPresent()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();

            CreateEntityController controller = new CreateEntityController(typeof(IdemOwner), "dp_idem");

            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            connection.DoesObjectExist("dp_idem_props", null, "table").Should().BeTrue();

            // second run: owner and props already exist -> must be a clean no-op
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            connection.DoesObjectExist("dp_idem_props", null, "table").Should().BeTrue();
        }

        [Fact]
        public void UpdateTables_DoesNotDropCoincidentallyNamedTable()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();

            new CreateEntityController(typeof(FalsePositiveOwner), "dp_fp")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            // a user table that happens to be named dp_fp_props but is NOT an EAV side table
            TableDescriptor fake = new TableDescriptor("dp_fp_props");
            fake.Add(new TableDescriptor.ColumnInfo() { Name = "id", DbType = DbType.Int32, PrimaryKey = true });
            fake.Add(new TableDescriptor.ColumnInfo() { Name = "data", DbType = DbType.String, Size = 16, Nullable = true });
            using (var query = connection.GetQuery(connection.GetCreateTableBuilder(fake)))
                query.ExecuteNoData();

            new CreateEntityController(typeof(FalsePositiveOwner), "dp_fp")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            connection.DoesObjectExist("dp_fp_props", null, "table")
                      .Should().BeTrue("a coincidentally-named table without EAV signature columns must not be dropped");
        }
    }
}

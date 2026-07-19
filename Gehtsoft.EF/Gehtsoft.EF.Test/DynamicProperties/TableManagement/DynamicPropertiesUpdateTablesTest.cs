using System.Data;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Catalog;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.TableManagement
{
    // Dynamic-properties schema migration is exercised through the current CatalogEntityController. Because
    // the catalogue reconciles against its own recorded state (not the live DB), an incremental V1->V2
    // migration is set up by building the V1 shape, seeding the target scope's catalogue with it, then
    // running UpdateTables against the V2 model (see CatalogTestSupport).
    public class DynamicPropertiesUpdateTablesTest
    {
        private static readonly Assembly Asm = typeof(DynamicPropertiesUpdateTablesTest).Assembly;

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
            CatalogTestSupport.ResetCatalog(connection, Asm);

            // Build the "before" (no dynamic properties) shape.
            new CatalogEntityController(typeof(GainBefore), "dp_gain_before").CreateTables(connection, "1.0.0");
            connection.DoesObjectExist("dp_gain", null, "table").Should().BeTrue();
            connection.DoesObjectExist("dp_gain_props", null, "table").Should().BeFalse();

            // Seed the target scope with the "before" shape, then migrate to the model that HAS props.
            CatalogTestSupport.Seed(connection, "dp_gain_after", "dp_gain", typeof(GainBefore), "1.0.0");
            new CatalogEntityController(typeof(GainAfter), "dp_gain_after")
                .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            connection.DoesObjectExist("dp_gain", null, "table").Should().BeTrue();
            connection.DoesObjectExist("dp_gain_props", null, "table").Should().BeTrue();

            foreach (string index in new[] { "owner", "owner_name", "name_str", "name_int", "name_real" })
                connection.DoesObjectExist("dp_gain_props", index, "index").Should().BeTrue($"index dp_gain_props_{index}");
        }

        [Fact]
        public void UpdateTables_LostDynamicProperties_DropsOrphanPropsTable()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            CatalogTestSupport.ResetCatalog(connection, Asm);

            // Build the "before" (with dynamic properties) shape.
            new CatalogEntityController(typeof(LostBefore), "dp_lost_before").CreateTables(connection, "1.0.0");
            connection.DoesObjectExist("dp_lost", null, "table").Should().BeTrue();
            connection.DoesObjectExist("dp_lost_props", null, "table").Should().BeTrue();

            // Seed the target scope with the "before" shape, then migrate to the model that LOST props.
            CatalogTestSupport.Seed(connection, "dp_lost_after", "dp_lost", typeof(LostBefore), "1.0.0");
            new CatalogEntityController(typeof(LostAfter), "dp_lost_after")
                .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            connection.DoesObjectExist("dp_lost", null, "table").Should().BeTrue("owner table must remain");
            connection.DoesObjectExist("dp_lost_props", null, "table").Should().BeFalse("orphan props table must be dropped");
        }

        [Fact]
        public void UpdateTables_Idempotent_WhenPropsAlreadyPresent()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            CatalogTestSupport.ResetCatalog(connection, Asm);

            var controller = new CatalogEntityController(typeof(IdemOwner), "dp_idem");

            // First contact creates the owner and its props side table.
            controller.UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);
            connection.DoesObjectExist("dp_idem_props", null, "table").Should().BeTrue();

            // Re-run at the same version with an unchanged model -> clean no-op.
            controller.UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);
            connection.DoesObjectExist("dp_idem_props", null, "table").Should().BeTrue();
        }

        [Fact]
        public void UpdateTables_DoesNotDropCoincidentallyNamedTable()
        {
            using SqlDbConnection connection = SqliteDbConnectionFactory.CreateMemory();
            CatalogTestSupport.ResetCatalog(connection, Asm);

            new CatalogEntityController(typeof(FalsePositiveOwner), "dp_fp")
                .UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            // a user table that happens to be named dp_fp_props but is NOT an EAV side table
            TableDescriptor fake = new TableDescriptor("dp_fp_props");
            fake.Add(new TableDescriptor.ColumnInfo() { Name = "id", DbType = DbType.Int32, PrimaryKey = true });
            fake.Add(new TableDescriptor.ColumnInfo() { Name = "data", DbType = DbType.String, Size = 16, Nullable = true });
            using (var query = connection.GetQuery(connection.GetCreateTableBuilder(fake)))
                query.ExecuteNoData();

            // A later deploy with an unchanged model: the catalogue only drops side tables it recorded, so a
            // coincidentally-named table it never catalogued is never touched.
            new CatalogEntityController(typeof(FalsePositiveOwner), "dp_fp")
                .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            connection.DoesObjectExist("dp_fp_props", null, "table")
                      .Should().BeTrue("a table the catalogue never recorded as an EAV side table must not be dropped");
        }
    }
}

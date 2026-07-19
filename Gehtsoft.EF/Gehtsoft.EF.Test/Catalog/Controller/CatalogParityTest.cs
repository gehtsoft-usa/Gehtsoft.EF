using System;
using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Catalog;
using Gehtsoft.EF.Db.SqlDb.Catalog.Store;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Catalog.Controller
{
    /// <summary>
    /// Phase 3 <b>increment 6 - the parity gate</b>. Runs each supported scenario through both the proven
    /// introspection-based <see cref="CreateEntityController"/> and the catalogue-based
    /// <see cref="CatalogEntityController"/> (with the <b>real</b> DDL action, not the mock the other
    /// controller tests use) on the same live connection, and asserts the two produce a
    /// <b>physically identical schema</b> - every table, column, index, view and dynamic-property side table
    /// exists (or not) the same way - plus a behavioural insert/select round-trip on the result.
    ///
    /// <para>Two divergences are <b>intended and decided</b>, not parity failures, so they are excluded here
    /// (and covered by dedicated refuse-tests in <see cref="CatalogEntityControllerTest"/>): a column
    /// <i>definition</i> change and a <i>unique single-column index</i> change. In both cases the old
    /// controller silently leaves the schema as-is (its <c>ReconcileIndexes</c> ignores unique/PK indexes;
    /// it has no portable in-place column modify), whereas the catalogue controller <i>refuses loudly</i>
    /// (<see cref="EfExceptionCode.CatalogColumnAlterNotSupported"/> / <see cref="NotSupportedException"/>)
    /// and routes the change to a patch. The catalogue surfaces what the old controller silently drops.</para>
    /// </summary>
    public sealed class CatalogParityTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private static readonly Assembly Asm = typeof(CatalogParityTest).Assembly;
        private static readonly CatalogSerializer Serializer = new CatalogSerializer();

        private readonly SqlConnectionFixtureBase mFixture;

        public CatalogParityTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        // ---- rich create-parity model (scope parity_create) --------------------------------------------

        private const string CreateScope = "parity_create";

        [Entity(Scope = CreateScope, Table = "par_dict")]
        public class ParDict
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }
        }

        public class ParMainMeta : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes
            {
                get
                {
                    var ix = new CompositeIndex("par_cmp")
                    {
                        nameof(ParMain.Name),
                        nameof(ParMain.Code),
                    };
                    yield return ix;
                }
            }
        }

        [Entity(Scope = CreateScope, Table = "par_main", Metadata = typeof(ParMainMeta))]
        public class ParMain
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            [EntityProperty(Sorted = true)]
            public int Code { get; set; }

            [ForeignKey]
            public ParDict Dict { get; set; }
        }

        public class ParViewMeta : IViewCreationMetadata
        {
            public SelectQueryBuilder GetSelectQuery(SqlDbConnection connection)
            {
                var td = AllEntities.Get<ParDict>().TableDescriptor;
                var builder = connection.GetSelectQueryBuilder(td);
                builder.AddToResultset(td[nameof(ParDict.Name)], nameof(ParDict.Name));
                return builder;
            }
        }

        [Entity(Scope = CreateScope, View = true, Metadata = typeof(ParViewMeta), Table = "par_view")]
        public class ParView
        {
            [EntityProperty(Nullable = true)]
            public string Name { get; set; }
        }

        // ---- dynamic-properties create-parity model (scope parity_dyn) ---------------------------------

        private const string DynScope = "parity_dyn";

        [Entity(Scope = DynScope, Table = "par_dyn_owner")]
        [DynamicProperties]
        public class ParDynOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        // ---- helpers -----------------------------------------------------------------------------------

        // A clean connection: drop ef_catalog so the catalogue starts at first contact, then re-establish
        // the infrastructure the catalogue controller expects (it no longer self-bootstraps).
        private SqlDbConnection Open(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            if (connection.DoesObjectExist("ef_catalog", null, "table"))
                using (var drop = connection.GetDropEntityQuery<EfCatalogRecord>())
                    drop.Execute();
            new CatalogEntityController(Asm).EnsureCatalogInfrastructure(connection);
            return connection;
        }

        // Physically drops every (obsolete-inclusive) table + view + dynamic-property side table of a scope,
        // via the proven old controller (side tables first - they FK the owner PK). Used to reset between the
        // old-controller run and the new-controller run within one test.
        private static void DropScopePhysical(SqlDbConnection connection, string scope)
        {
            EntityFinder.EntityTypeInfo[] types = EntityFinder.FindEntities(new[] { Asm }, scope, true);
            foreach (EntityFinder.EntityTypeInfo info in types)
            {
                if (info.View)
                    continue;
                EntityDescriptor ed = AllEntities.Inst[info.EntityType];
                if (ed.HasDynamicProperties)
                {
                    TableDescriptor side = ed.DynamicPropertiesTable;
                    if (connection.DoesObjectExist(side.Name, null, "table"))
                        using (var q = connection.GetQuery(connection.GetDropTableBuilder(side)))
                            q.ExecuteNoData();
                }
            }
            new CreateEntityControllerInternal(Asm, scope).DropTables(connection);
        }

        // The physical schema fingerprint of a scope: existence of every table / column / declared index /
        // view / dynamic-property side table, enumerated from the model so the two controllers are probed
        // identically. Two fingerprints being equal is the parity assertion.
        private static SortedDictionary<string, bool> Fingerprint(SqlDbConnection connection, string scope,
            IReadOnlyList<(string table, string obj, string type)> extra = null)
        {
            var fp = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            if (extra != null)
                foreach ((string table, string obj, string type) in extra)
                    fp["extra:" + table + "." + obj + "." + type] = connection.DoesObjectExist(table, obj, type);
            EntityFinder.EntityTypeInfo[] types = EntityFinder.FindEntities(new[] { Asm }, scope, true);
            foreach (EntityFinder.EntityTypeInfo info in types)
            {
                string table = info.Table;
                if (info.View)
                {
                    fp["view:" + table] = connection.DoesObjectExist(table, null, "view");
                    continue;
                }
                fp["table:" + table] = connection.DoesObjectExist(table, null, "table");

                EntityDescriptor ed = AllEntities.Inst[info.EntityType];
                TableDescriptor td = ed.TableDescriptor;
                foreach (TableDescriptor.ColumnInfo col in td)
                {
                    fp["col:" + table + "." + col.Name] = connection.DoesObjectExist(table, col.Name, "column");
                    fp["idx:" + table + "." + col.Name] = connection.DoesObjectExist(table, col.Name, "index");
                }
                if (td.Metadata is ICompositeIndexMetadata meta)
                    foreach (CompositeIndex ci in meta.Indexes)
                        fp["cidx:" + table + "." + ci.Name] = connection.DoesObjectExist(table, ci.Name, "index");
                if (ed.HasDynamicProperties)
                {
                    string side = ed.DynamicPropertiesTable.Name;
                    fp["dptable:" + side] = connection.DoesObjectExist(side, null, "table");
                }
            }
            return fp;
        }

        private static void RichRoundTrip(SqlDbConnection connection)
        {
            var dict = new ParDict { Name = "d1" };
            using (var q = connection.GetInsertEntityQuery<ParDict>())
                q.Execute(dict);
            var main = new ParMain { Name = "m1", Code = 5, Dict = dict };
            using (var q = connection.GetInsertEntityQuery<ParMain>())
                q.Execute(main);
            using (var q = connection.GetSelectEntitiesCountQuery<ParMain>())
                q.RowCount.Should().Be(1);
        }

        // ---- create-from-scratch parity ----------------------------------------------------------------

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Create_RichModel_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, CreateScope);

            // Old controller (introspection): create everything from scratch.
            new CreateEntityControllerInternal(Asm, CreateScope).UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, CreateScope);
            RichRoundTrip(connection);

            // Reset physical; ef_catalog carries no rows for the scope (the old controller never writes it),
            // so the catalogue run below is a genuine first contact.
            DropScopePhysical(connection, CreateScope);

            // New controller (catalogue), real action: create everything from scratch.
            new CatalogEntityController(Asm, CreateScope).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, CreateScope);
            RichRoundTrip(connection);

            newFp.Should().BeEquivalentTo(oldFp);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateTables_RichModel_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, CreateScope);

            // CreateTables is the unconditional-create entry point. Both controllers create the tables AND
            // the view here (the catalogue's CreateTables was fixed in increment 6 to materialize views, like
            // the old controller, so it is a drop-in replacement).
            new CreateEntityControllerInternal(Asm, CreateScope).CreateTables(connection);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, CreateScope);
            RichRoundTrip(connection);

            DropScopePhysical(connection, CreateScope);

            new CatalogEntityController(Asm, CreateScope).CreateTables(connection, "1.0.0");
            SortedDictionary<string, bool> newFp = Fingerprint(connection, CreateScope);
            RichRoundTrip(connection);

            newFp.Should().BeEquivalentTo(oldFp);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Recreate_RichModel_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, CreateScope);

            new CreateEntityControllerInternal(Asm, CreateScope).UpdateTables(connection, EntityUpdateMode.Recreate);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, CreateScope);
            RichRoundTrip(connection);

            DropScopePhysical(connection, CreateScope);

            new CatalogEntityController(Asm, CreateScope).UpdateTables(connection, "1.0.0", EntityUpdateMode.Recreate);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, CreateScope);
            RichRoundTrip(connection);

            newFp.Should().BeEquivalentTo(oldFp);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Create_DynamicProperties_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, DynScope);

            new CreateEntityControllerInternal(Asm, DynScope).UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, DynScope);

            DropScopePhysical(connection, DynScope);

            new CatalogEntityController(Asm, DynScope).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, DynScope);

            newFp.Should().BeEquivalentTo(oldFp);
        }

        // ---- evolution parity --------------------------------------------------------------------------
        //
        // The catalogue reconciles against its own stored state, not the live DB, so an incremental
        // (V1 -> V2) run cannot be reached with a second entity model on the same connection the way the old
        // controller manages it. Each evolution scenario therefore:
        //   old path : old(V1) build, then old(V2) reconcile  -> the introspection-produced schema.
        //   new path : old(V1) build the same starting DB, seed the catalogue with the V1 snapshot under the
        //              V2 scope, then new(V2) UpdateTables("2.0.0") -> the catalogue-produced schema.
        // Both are fingerprinted over the V2 model and must match. V1/V2 share table names (as the existing
        // create/update scenarios do), so the two paths converge the same physical table.

        // Builds the catalogue DTO the controller would compute for a type (composite indexes + dynamic-
        // properties flag), overriding the scope so a V1-shaped snapshot can be seeded under the V2 scope.
        private static CatalogTableDto DtoFor(Type type, string scopeOverride)
        {
            EntityDescriptor ed = AllEntities.Inst[type];
            TableDescriptor descriptor = ed.TableDescriptor;
            List<CompositeIndex> composite = null;
            if (descriptor.Metadata is ICompositeIndexMetadata metadata)
            {
                composite = new List<CompositeIndex>();
                foreach (CompositeIndex index in metadata.Indexes)
                    composite.Add(index);
            }
            CatalogTableDto dto = Serializer.FromDescriptor(descriptor, composite);
            dto.HasDynamicProperties = ed.HasDynamicProperties;
            dto.Scope = scopeOverride;
            return dto;
        }

        private static void SeedV1(SqlDbConnection connection, string targetScope, string table, Type v1Type)
            => new CatalogStore().WriteApplied(connection, targetScope, table, "1.0.0", DtoFor(v1Type, targetScope));

        // Add column ------------------------------------------------------------------------------------
        [Entity(Scope = "parity_addc_v1", Table = "par_addc")]
        public class AddcV1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }
        }

        [Entity(Scope = "parity_addc", Table = "par_addc")]
        public class AddcV2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            [EntityProperty(Nullable = true)]
            public int? Extra { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Evolve_AddColumn_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, "parity_addc");

            new CreateEntityControllerInternal(Asm, "parity_addc_v1").UpdateTables(connection, EntityUpdateMode.Update);
            new CreateEntityControllerInternal(Asm, "parity_addc").UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, "parity_addc");

            DropScopePhysical(connection, "parity_addc");

            new CreateEntityControllerInternal(Asm, "parity_addc_v1").UpdateTables(connection, EntityUpdateMode.Update);
            SeedV1(connection, "parity_addc", "par_addc", typeof(AddcV1));
            new CatalogEntityController(Asm, "parity_addc").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, "parity_addc");

            newFp.Should().BeEquivalentTo(oldFp);
        }

        // Drop obsolete property (column) ---------------------------------------------------------------
        [Entity(Scope = "parity_dropc_v1", Table = "par_dropc")]
        public class DropcV1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            [EntityProperty(Nullable = true)]
            public int? Legacy { get; set; }
        }

        [Entity(Scope = "parity_dropc", Table = "par_dropc")]
        public class DropcV2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            [ObsoleteEntityProperty]
            public int? Legacy { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Evolve_DropObsoleteProperty_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            var legacyProbe = new[] { ("par_dropc", "legacy", "column") };
            DropScopePhysical(connection, "parity_dropc");

            new CreateEntityControllerInternal(Asm, "parity_dropc_v1").UpdateTables(connection, EntityUpdateMode.Update);
            new CreateEntityControllerInternal(Asm, "parity_dropc").UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, "parity_dropc", legacyProbe);

            DropScopePhysical(connection, "parity_dropc");

            new CreateEntityControllerInternal(Asm, "parity_dropc_v1").UpdateTables(connection, EntityUpdateMode.Update);
            SeedV1(connection, "parity_dropc", "par_dropc", typeof(DropcV1));
            new CatalogEntityController(Asm, "parity_dropc").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, "parity_dropc", legacyProbe);

            newFp.Should().BeEquivalentTo(oldFp);
        }

        // Drop obsolete entity (table) -----------------------------------------------------------------
        [Entity(Scope = "parity_drope_v1", Table = "par_drope_live")]
        public class DropeLiveV1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int V { get; set; }
        }

        [Entity(Scope = "parity_drope_v1", Table = "par_drope_gone")]
        public class DropeGoneV1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int W { get; set; }
        }

        [Entity(Scope = "parity_drope", Table = "par_drope_live")]
        public class DropeLiveV2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int V { get; set; }
        }

        [ObsoleteEntity(Scope = "parity_drope", Table = "par_drope_gone")]
        public class DropeGoneV2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int W { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Evolve_DropObsoleteEntity_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, "parity_drope");

            new CreateEntityControllerInternal(Asm, "parity_drope_v1").UpdateTables(connection, EntityUpdateMode.Update);
            new CreateEntityControllerInternal(Asm, "parity_drope").UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, "parity_drope");

            DropScopePhysical(connection, "parity_drope");

            new CreateEntityControllerInternal(Asm, "parity_drope_v1").UpdateTables(connection, EntityUpdateMode.Update);
            SeedV1(connection, "parity_drope", "par_drope_live", typeof(DropeLiveV1));
            SeedV1(connection, "parity_drope", "par_drope_gone", typeof(DropeGoneV1));
            new CatalogEntityController(Asm, "parity_drope").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, "parity_drope");

            newFp.Should().BeEquivalentTo(oldFp);
        }

        // Add single-column sorted index ---------------------------------------------------------------
        [Entity(Scope = "parity_addi_v1", Table = "par_addi")]
        public class AddiV1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Code { get; set; }
        }

        [Entity(Scope = "parity_addi", Table = "par_addi")]
        public class AddiV2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Sorted = true)]
            public int Code { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Evolve_AddIndex_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, "parity_addi");

            new CreateEntityControllerInternal(Asm, "parity_addi_v1").UpdateTables(connection, EntityUpdateMode.Update);
            new CreateEntityControllerInternal(Asm, "parity_addi").UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, "parity_addi");

            DropScopePhysical(connection, "parity_addi");

            new CreateEntityControllerInternal(Asm, "parity_addi_v1").UpdateTables(connection, EntityUpdateMode.Update);
            SeedV1(connection, "parity_addi", "par_addi", typeof(AddiV1));
            new CatalogEntityController(Asm, "parity_addi").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, "parity_addi");

            newFp.Should().BeEquivalentTo(oldFp);
        }

        // Drop single-column sorted index --------------------------------------------------------------
        [Entity(Scope = "parity_dropi_v1", Table = "par_dropi")]
        public class DropiV1
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Sorted = true)]
            public int Code { get; set; }
        }

        [Entity(Scope = "parity_dropi", Table = "par_dropi")]
        public class DropiV2
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Code { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Evolve_DropIndex_Parity(string connectionName)
        {
            var connection = Open(connectionName);
            DropScopePhysical(connection, "parity_dropi");

            new CreateEntityControllerInternal(Asm, "parity_dropi_v1").UpdateTables(connection, EntityUpdateMode.Update);
            new CreateEntityControllerInternal(Asm, "parity_dropi").UpdateTables(connection, EntityUpdateMode.Update);
            SortedDictionary<string, bool> oldFp = Fingerprint(connection, "parity_dropi");

            DropScopePhysical(connection, "parity_dropi");

            new CreateEntityControllerInternal(Asm, "parity_dropi_v1").UpdateTables(connection, EntityUpdateMode.Update);
            SeedV1(connection, "parity_dropi", "par_dropi", typeof(DropiV1));
            new CatalogEntityController(Asm, "parity_dropi").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            SortedDictionary<string, bool> newFp = Fingerprint(connection, "parity_dropi");

            newFp.Should().BeEquivalentTo(oldFp);
        }
    }
}

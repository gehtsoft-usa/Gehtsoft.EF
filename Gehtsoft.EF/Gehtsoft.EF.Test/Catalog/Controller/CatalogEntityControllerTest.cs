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
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.Catalog.Controller
{
    /// <summary>
    /// Phase 3 increment-1 behavioural tests for <see cref="CatalogEntityController"/> over every live
    /// driver. The DDL action surface is mocked so the tests assert the controller's *decisions*
    /// (create / add-column) driver-agnostically, while the catalogue reads/writes and the scope-level
    /// version guard run against the real <see cref="CatalogStore"/> on each connection. Each test drops
    /// <c>ef_catalog</c> first for a clean start.
    /// </summary>
    public sealed class CatalogEntityControllerTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private const string Scope = "cat_ctrl";
        private static readonly CatalogSerializer Serializer = new CatalogSerializer();

        private readonly SqlConnectionFixtureBase mFixture;

        public CatalogEntityControllerTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        [Entity(Scope = Scope, Table = "cat_ctrl_a")]
        public class CtrlA
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }
        }

        [Entity(Scope = Scope, Table = "cat_ctrl_b")]
        public class CtrlB
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Weight { get; set; }
        }

        private const string HookScope = "cat_ctrl_hook";
        private static bool HookCreateCalled { get; set; }
        private static void OnHookCreate(SqlDbConnection _) => HookCreateCalled = true;

        [OnEntityCreate(typeof(CatalogEntityControllerTest), nameof(OnHookCreate))]
        [Entity(Scope = HookScope, Table = "cat_ctrl_hook_a")]
        public class HookEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Value { get; set; }
        }

        private const string IdxScope = "cat_ctrl_idx";

        [Entity(Scope = IdxScope, Table = "cat_ctrl_idx_a")]
        public class IdxEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Sorted = true)]
            public int Code { get; set; }

            [EntityProperty]
            public int Val { get; set; }
        }

        private static CatalogTableDto DesiredIdx() => Serializer.FromDescriptor(AllEntities.Inst[typeof(IdxEntity)].TableDescriptor, null);

        private static CatalogColumnDto SortedColumn(CatalogTableDto dto)
        {
            foreach (var c in dto.Columns)
                if (c.Sorted)
                    return c;
            throw new InvalidOperationException("no sorted column");
        }

        private static CatalogColumnDto PlainColumn(CatalogTableDto dto)
        {
            foreach (var c in dto.Columns)
                if (!c.Sorted && !c.PrimaryKey && !c.Autoincrement)
                    return c;
            throw new InvalidOperationException("no plain column");
        }

        private CatalogEntityController IdxController(Mock<CatalogEntityController.ICatalogControllerAction> action)
            => new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly, IdxScope) { ActionController = action.Object };

        // FK scope: a child holding an active foreign key to a parent (for the recreate guard).
        private const string FkScope = "cat_ctrl_fk";

        [Entity(Scope = FkScope, Table = "cat_ctrl_fk_parent")]
        public class FkParent
        {
            [AutoId]
            public int Id { get; set; }
        }

        [Entity(Scope = FkScope, Table = "cat_ctrl_fk_child")]
        public class FkChild
        {
            [AutoId]
            public int Id { get; set; }

            [ForeignKey]
            public FkParent Parent { get; set; }
        }

        // Obsolete scope: one live entity and one [ObsoleteEntity] whose table must be dropped+tombstoned.
        private const string ObsScope = "cat_ctrl_obs";

        [Entity(Scope = ObsScope, Table = "cat_ctrl_obs_live")]
        public class ObsLive
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Val { get; set; }
        }

        [ObsoleteEntity(Scope = ObsScope, Table = "cat_ctrl_obs_gone")]
        public class ObsGone
        {
            [AutoId]
            public int Id { get; set; }
        }

        // Property-drop scope: an entity with an obsolete property carrying an OnEntityPropertyDrop hook.
        private const string PropDropScope = "cat_ctrl_pdrop";
        private static bool PropDropCalled { get; set; }
        private static void OnPropDrop(SqlDbConnection _) => PropDropCalled = true;

        [Entity(Scope = PropDropScope, Table = "cat_ctrl_pdrop_a")]
        public class PDropEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Keep { get; set; }

            [ObsoleteEntityProperty]
            [OnEntityPropertyDrop(typeof(CatalogEntityControllerTest), nameof(OnPropDrop))]
            public int Gone { get; set; }
        }

        // View scope.
        private const string ViewScope = "cat_ctrl_view";

        [Entity(Scope = ViewScope, View = true, Metadata = typeof(IViewCreationMetadata), Table = "cat_ctrl_view_v")]
        public class ViewEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int X { get; set; }
        }

        private CatalogEntityController ScopedController(string scope, Mock<CatalogEntityController.ICatalogControllerAction> action)
            => new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly, scope) { ActionController = action.Object };

        // Dynamic-properties scope: an owner that carries an EAV side table.
        private const string DpScope = "cat_ctrl_dp";

        [Entity(Scope = DpScope, Table = "cat_ctrl_dp_owner")]
        [DynamicProperties]
        public class DynOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        // Adoption scope: two independent tables used to simulate a pre-catalogue database and adopt it.
        private const string AdoptScope = "cat_adopt";

        [Entity(Scope = AdoptScope, Table = "cat_adopt_a")]
        public class AdoptA
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            [EntityProperty(Sorted = true)]
            public int Code { get; set; }
        }

        [Entity(Scope = AdoptScope, Table = "cat_adopt_b")]
        public class AdoptB
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int Value { get; set; }
        }

        private static void DropAdoptTables(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("cat_adopt_b", null, "table"))
                using (var q = connection.GetDropEntityQuery<AdoptB>())
                    q.Execute();
            if (connection.DoesObjectExist("cat_adopt_a", null, "table"))
                using (var q = connection.GetDropEntityQuery<AdoptA>())
                    q.Execute();
        }

        // Dedicated adoption scope for the dynamic-properties verify-drift test (its own tables so the
        // plain-adoption tests above are unaffected).
        private const string AdoptDpScope = "cat_adopt_dp";

        [Entity(Scope = AdoptDpScope, Table = "cat_adopt_dp_owner")]
        [DynamicProperties]
        public class AdoptDpOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static void DropAdoptDpTables(SqlDbConnection connection)
        {
            TableDescriptor side = AllEntities.Inst[typeof(AdoptDpOwner)].DynamicPropertiesTable;
            if (connection.DoesObjectExist(side.Name, null, "table"))
                using (var q = connection.GetQuery(connection.GetDropTableBuilder(side)))
                    q.ExecuteNoData();
            if (connection.DoesObjectExist("cat_adopt_dp_owner", null, "table"))
                using (var q = connection.GetDropEntityQuery<AdoptDpOwner>())
                    q.Execute();
        }

        private static CatalogEntityController AdoptController()
            => new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly, AdoptScope);

        private SqlDbConnection Open(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            if (connection.DoesObjectExist("ef_catalog", null, "table"))
                using (var drop = connection.GetDropEntityQuery<EfCatalogRecord>())
                    drop.Execute();
            // The controller no longer self-bootstraps; the caller establishes the infrastructure once.
            new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly).EnsureCatalogInfrastructure(connection);
            return connection;
        }

        // The controller replays coded patches via the injected action on every apply path; strict mocks
        // that reach it must allow the call.
        private static void AllowReplay(Mock<CatalogEntityController.ICatalogControllerAction> action)
            => action.Setup(x => x.ReplayPatches(It.IsAny<SqlDbConnection>(), It.IsAny<IEnumerable<Assembly>>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()));

        private static void VerifyReplayed(Mock<CatalogEntityController.ICatalogControllerAction> action)
            => action.Verify(x => x.ReplayPatches(It.IsAny<SqlDbConnection>(), It.IsAny<IEnumerable<Assembly>>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()), Times.Once);

        private static long VersionKeyOf(string version)
        {
            string[] p = version.Split('.');
            return (long)int.Parse(p[0]) * 10000000 + (long)int.Parse(p[1]) * 10000 + int.Parse(p[2]);
        }

        private static CatalogEntityController Controller(Mock<CatalogEntityController.ICatalogControllerAction> action)
            => new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly, Scope)
            {
                ActionController = action.Object,
            };

        // The full desired snapshot the controller computes for an entity (no composite indexes here).
        private static CatalogTableDto DesiredDto<T>()
            => Serializer.FromDescriptor(AllEntities.Inst[typeof(T)].TableDescriptor, null);

        private static void Seed(CatalogStore store, SqlDbConnection connection, string table, string version, CatalogTableDto dto)
            => store.WriteApplied(connection, Scope, table, version, dto);

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_FirstContact_CreatesEachTable_AndRecordsVersion(string connectionName)
        {
            var connection = Open(connectionName);
            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();

            Controller(action).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            // Both tables are created (first contact = diff against nothing = CreateTable).
            action.Verify(x => x.Create(connection, It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_a")), Times.Once);
            action.Verify(x => x.Create(connection, It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_b")), Times.Once);
            action.Verify(x => x.AddColumns(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>(), It.IsAny<TableDescriptor>(), It.IsAny<TableDescriptor.ColumnInfo[]>()), Times.Never);

            var store = new CatalogStore();
            var applied = store.ReadAppliedForScope(connection, Scope);
            applied.Count.Should().Be(2);
            applied.ContainsKey("cat_ctrl_a").Should().BeTrue();
            applied.ContainsKey("cat_ctrl_b").Should().BeTrue();
            store.ReadCurrentVersion(connection, Scope).Should().Be("1.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_SameVersionUnchanged_IsCleanNoOp(string connectionName)
        {
            var connection = Open(connectionName);

            // First run populates the catalogue.
            Controller(new Mock<CatalogEntityController.ICatalogControllerAction>())
                .UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            // A re-run at the same version with an unchanged model must touch nothing.
            var strict = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            Action act = () => Controller(strict).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            act.Should().NotThrow();
            strict.VerifyNoOtherCalls();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_LowerVersion_ThrowsRegression(string connectionName)
        {
            var connection = Open(connectionName);
            Controller(new Mock<CatalogEntityController.ICatalogControllerAction>())
                .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            var strict = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            Action act = () => Controller(strict).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CatalogVersionRegressed);
            strict.VerifyNoOtherCalls();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_ModelChangedWithoutVersionBump_Throws(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Seed the catalogue at 1.0.0 with A missing its non-key column (simulating a model that has
            // since gained one) and B matching, all at the same version.
            CatalogTableDto oldA = DesiredDto<CtrlA>();
            oldA.Columns.RemoveAt(oldA.Columns.Count - 1);
            Seed(store, connection, "cat_ctrl_a", "1.0.0", oldA);
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var strict = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            Action act = () => Controller(strict).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CatalogModelChangedWithoutVersionBump);
            strict.VerifyNoOtherCalls();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_AddsColumn_OnVersionBump_AndRecords(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state at 1.0.0: A missing its non-key column, B already matching the model.
            CatalogTableDto oldA = DesiredDto<CtrlA>();
            string addedColumn = oldA.Columns[oldA.Columns.Count - 1].Name;
            oldA.Columns.RemoveAt(oldA.Columns.Count - 1);
            Seed(store, connection, "cat_ctrl_a", "1.0.0", oldA);
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // A gains exactly the missing column; nothing is created, B is untouched by DDL.
            action.Verify(x => x.AddColumns(connection,
                It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_a"),
                It.IsAny<TableDescriptor>(),
                It.Is<TableDescriptor.ColumnInfo[]>(c => c.Length == 1 && c[0].Name == addedColumn)), Times.Once);
            action.Verify(x => x.Create(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Never);

            // A recorded at 2.0.0 with the full snapshot; B advanced to 2.0.0 in place (no new row).
            store.ReadCurrentVersion(connection, Scope).Should().Be("2.0.0");
            var applied = store.ReadAppliedForScope(connection, Scope);
            Serializer.Serialize(new CatalogSnapshot { Table = applied["cat_ctrl_a"] })
                .Should().Be(Serializer.Serialize(new CatalogSnapshot { Table = DesiredDto<CtrlA>() }));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_UnchangedTables_AdvanceVersionInPlace(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state at 1.0.0 matches the model exactly.
            Seed(store, connection, "cat_ctrl_a", "1.0.0", DesiredDto<CtrlA>());
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var strict = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            AllowReplay(strict);
            Controller(strict).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // No DDL for unchanged tables; both live rows advanced to the new version in place. Only the
            // post-convergence patch replay is expected.
            VerifyReplayed(strict);
            strict.VerifyNoOtherCalls();
            store.ReadCurrentVersion(connection, Scope).Should().Be("2.0.0");
            CountRows(connection).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_DropsColumn_OnVersionBump(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state at 1.0.0: A carries an extra column no longer in the model; B matches.
            CatalogTableDto oldA = DesiredDto<CtrlA>();
            oldA.Columns.Add(new CatalogColumnDto { Id = "Legacy", Name = "legacy_col", DbType = "Int32" });
            Seed(store, connection, "cat_ctrl_a", "1.0.0", oldA);
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            // legacy_col has no [ObsoleteEntityProperty] marker, so the drop is an implicit data loss; opt in.
            var controller = Controller(action);
            controller.DataLossPolicy = CatalogEntityController.CatalogDataLossPolicy.Drop;
            controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // A driver that cannot drop columns (e.g. SQLite) leaves the column in place, matching
            // CreateEntityController; the version still advances (the desired snapshot is recorded).
            bool canDrop = connection.GetLanguageSpecifics().DropColumnSupported;
            action.Verify(x => x.DropColumns(connection,
                It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_a"),
                It.IsAny<TableDescriptor>(),
                It.Is<TableDescriptor.ColumnInfo[]>(c => c.Length == 1 && c[0].Name == "legacy_col")), canDrop ? Times.Once() : Times.Never());
            action.Verify(x => x.Create(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Never);
            store.ReadCurrentVersion(connection, Scope).Should().Be("2.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_ColumnDefinitionChange_IsRefused(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state at 1.0.0: A's non-key column has a different size than the model declares.
            CatalogTableDto oldA = DesiredDto<CtrlA>();
            oldA.Columns[oldA.Columns.Count - 1].Size += 32;
            Seed(store, connection, "cat_ctrl_a", "1.0.0", oldA);
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Action act = () => Controller(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CatalogColumnAlterNotSupported);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_FirstContact_InvokesOnEntityCreateHook(string connectionName)
        {
            var connection = Open(connectionName);
            HookCreateCalled = false;

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly, HookScope)
            {
                ActionController = action.Object,
            }.UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            HookCreateCalled.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_CreatesSingleColumnIndex_OnVersionBump(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state: the model's sorted column was NOT sorted before -> the index must be created.
            CatalogTableDto old = DesiredIdx();
            string indexColumn = SortedColumn(old).Name;
            SortedColumn(old).Sorted = false;
            store.WriteApplied(connection, IdxScope, "cat_ctrl_idx_a", "1.0.0", old);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            IdxController(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.CreateIndex(connection, It.IsAny<TableDescriptor>(),
                It.Is<CompositeIndex>(ci => ci.Name == indexColumn)), Times.Once);
            store.ReadCurrentVersion(connection, IdxScope).Should().Be("2.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_DropsSingleColumnIndex_OnVersionBump(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state: a plain column carried a sorted index the model no longer declares.
            CatalogTableDto old = DesiredIdx();
            string indexColumn = PlainColumn(old).Name;
            PlainColumn(old).Sorted = true;
            store.WriteApplied(connection, IdxScope, "cat_ctrl_idx_a", "1.0.0", old);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            IdxController(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.DropIndex(connection, It.IsAny<TableDescriptor>(), indexColumn), Times.Once);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_UniqueSingleColumnIndexChange_IsNotSupportedYet(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state carries a unique index on a column the model does not mark unique.
            CatalogTableDto old = DesiredIdx();
            PlainColumn(old).Unique = true;
            store.WriteApplied(connection, IdxScope, "cat_ctrl_idx_a", "1.0.0", old);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Action act = () => IdxController(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            act.Should().Throw<NotSupportedException>();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_RecreateMode_DropsAndRecreates(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);
            Seed(store, connection, "cat_ctrl_a", "1.0.0", DesiredDto<CtrlA>());
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Recreate);

            // Each existing table is dropped then recreated.
            action.Verify(x => x.Drop(connection, It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_a")), Times.Once);
            action.Verify(x => x.Create(connection, It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_a")), Times.Once);
            store.ReadCurrentVersion(connection, Scope).Should().Be("2.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_RecreateMode_FirstContact_CreatesWithoutDrop(string connectionName)
        {
            var connection = Open(connectionName);
            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).UpdateTables(connection, "1.0.0", EntityUpdateMode.Recreate);

            action.Verify(x => x.Create(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Exactly(2));
            action.Verify(x => x.Drop(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_CreateNewMode_BehavesLikeUpdate(string connectionName)
        {
            var connection = Open(connectionName);
            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).UpdateTables(connection, "1.0.0", EntityUpdateMode.CreateNew);

            action.Verify(x => x.Create(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Exactly(2));
            new CatalogStore().ReadCurrentVersion(connection, Scope).Should().Be("1.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_RecreatingReferencedTable_ThrowsWhenDependentSurvives(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);
            Seed(store, connection, "cat_ctrl_fk_parent", "1.0.0", Serializer.FromDescriptor(AllEntities.Inst[typeof(FkParent)].TableDescriptor, null));
            Seed(store, connection, "cat_ctrl_fk_child", "1.0.0", Serializer.FromDescriptor(AllEntities.Inst[typeof(FkChild)].TableDescriptor, null));

            var perType = new Dictionary<Type, EntityUpdateMode>
            {
                [typeof(FkParent)] = EntityUpdateMode.Recreate,
            };

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            Action act = () => ScopedController(FkScope, action)
                .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update, perType);

            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CannotRecreateTable);
            action.VerifyNoOtherCalls();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_ObsoleteEntity_DroppedAndTombstoned(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Both tables catalogued; the obsolete one must be dropped and read as absent afterwards.
            store.WriteApplied(connection, ObsScope, "cat_ctrl_obs_live", "1.0.0", Serializer.FromDescriptor(AllEntities.Inst[typeof(ObsLive)].TableDescriptor, null));
            var goneDto = new CatalogTableDto { Name = "cat_ctrl_obs_gone", Scope = ObsScope };
            goneDto.Columns.Add(new CatalogColumnDto { Id = "Id", Name = "id", DbType = "Int32", PrimaryKey = true, Autoincrement = true });
            store.WriteApplied(connection, ObsScope, "cat_ctrl_obs_gone", "1.0.0", goneDto);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            ScopedController(ObsScope, action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.Drop(connection, It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_obs_gone")), Times.Once);
            store.ReadApplied(connection, ObsScope, "cat_ctrl_obs_gone").Should().BeNull();
            store.ReadAppliedForScope(connection, ObsScope).ContainsKey("cat_ctrl_obs_live").Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_ObsoleteProperty_DropsColumn_AndFiresHook(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);
            PropDropCalled = false;

            // Seed the prior shape WITH the now-obsolete column so the diff drops it.
            CatalogTableDto old = Serializer.FromDescriptor(AllEntities.Inst[typeof(PDropEntity)].TableDescriptor, null);
            old.Columns.Add(new CatalogColumnDto { Id = "Gone", Name = "gone", DbType = "Int32" });
            store.WriteApplied(connection, PropDropScope, "cat_ctrl_pdrop_a", "1.0.0", old);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            ScopedController(PropDropScope, action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // On a driver that cannot drop columns the drop (and its hook) are skipped, matching
            // CreateEntityController; elsewhere the column is dropped and OnEntityPropertyDrop fires.
            bool canDrop = connection.GetLanguageSpecifics().DropColumnSupported;
            action.Verify(x => x.DropColumns(connection,
                It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_pdrop_a"),
                It.IsAny<TableDescriptor>(),
                It.Is<TableDescriptor.ColumnInfo[]>(c => c.Length == 1 && c[0].Name == "gone")), canDrop ? Times.Once() : Times.Never());
            PropDropCalled.Should().Be(canDrop);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_GainDynamicProperties_CreatesSideTable(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state: the owner existed without a dynamic-property side table.
            CatalogTableDto old = Serializer.FromDescriptor(AllEntities.Inst[typeof(DynOwner)].TableDescriptor, null);
            old.HasDynamicProperties = false;
            store.WriteApplied(connection, DpScope, "cat_ctrl_dp_owner", "1.0.0", old);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            ScopedController(DpScope, action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.CreateDynamicPropertiesTable(connection,
                It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_dp_owner")), Times.Once);
            action.Verify(x => x.DropDynamicPropertiesTable(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Never);
            store.ReadCurrentVersion(connection, DpScope).Should().Be("2.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_LoseDynamicProperties_DropsSideTable(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state: a (now-plain) table used to carry a dynamic-property side table.
            CatalogTableDto oldA = DesiredDto<CtrlA>();
            oldA.HasDynamicProperties = true;
            store.WriteApplied(connection, Scope, "cat_ctrl_a", "1.0.0", oldA);
            store.WriteApplied(connection, Scope, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            // Dropping the dynamic-properties side table loses its values; opt into the loss.
            var controller = Controller(action);
            controller.DataLossPolicy = CatalogEntityController.CatalogDataLossPolicy.Drop;
            controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.DropDynamicPropertiesTable(connection,
                It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_a")), Times.Once);
            action.Verify(x => x.CreateDynamicPropertiesTable(It.IsAny<SqlDbConnection>(), It.IsAny<EntityFinder.EntityTypeInfo>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_UnmarkedColumnDrop_FailsByDefault(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // A carries a column no longer in the model and NOT marked [ObsoleteEntityProperty].
            CatalogTableDto oldA = DesiredDto<CtrlA>();
            oldA.Columns.Add(new CatalogColumnDto { Id = "Legacy", Name = "legacy_col", DbType = "Int32" });
            Seed(store, connection, "cat_ctrl_a", "1.0.0", oldA);
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            // Default DataLossPolicy is Fail: the implicit column drop is refused before any DDL, atomically.
            var strict = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            Action act = () => Controller(strict).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.CatalogColumnDropWouldLoseData);
            strict.VerifyNoOtherCalls();
            store.ReadCurrentVersion(connection, Scope).Should().Be("1.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_LoseDynamicProperties_FailsByDefault(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            CatalogTableDto oldA = DesiredDto<CtrlA>();
            oldA.HasDynamicProperties = true;
            store.WriteApplied(connection, Scope, "cat_ctrl_a", "1.0.0", oldA);
            store.WriteApplied(connection, Scope, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            // Default DataLossPolicy is Fail: dropping the dynamic-properties side table is refused.
            var strict = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            Action act = () => Controller(strict).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.CatalogDynamicPropertiesDropWouldLoseData);
            strict.VerifyNoOtherCalls();
            store.ReadCurrentVersion(connection, Scope).Should().Be("1.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_DynamicPropertiesUnchanged_TouchesNoSideTable(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Prior state already had the side table (HasDynamicProperties matches the model).
            CatalogTableDto old = Serializer.FromDescriptor(AllEntities.Inst[typeof(DynOwner)].TableDescriptor, null);
            old.HasDynamicProperties = true;
            store.WriteApplied(connection, DpScope, "cat_ctrl_dp_owner", "1.0.0", old);

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>(MockBehavior.Strict);
            AllowReplay(action);
            ScopedController(DpScope, action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // Unchanged: only an in-place version advance and the post-convergence patch replay, no
            // side-table DDL.
            VerifyReplayed(action);
            action.VerifyNoOtherCalls();
            store.ReadCurrentVersion(connection, DpScope).Should().Be("2.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_View_IsDroppedAndRecreated_NotCatalogued(string connectionName)
        {
            var connection = Open(connectionName);
            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();

            ScopedController(ViewScope, action).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.Create(connection, It.Is<EntityFinder.EntityTypeInfo>(e => e.Table == "cat_ctrl_view_v")), Times.Once);
            // Views are not tracked in the catalogue.
            new CatalogStore().ReadAppliedForScope(connection, ViewScope).Count.Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_FirstContact_DoesNotReplayPatches(string connectionName)
        {
            var connection = Open(connectionName);
            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();

            Controller(action).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            // First contact runs NO patches: the freshly-created structure already reflects everything up to
            // the target version. This matches CreateTables and EfPatchProcessor's stamp-and-run-none on a
            // fresh database - a fresh DB has nothing to migrate. Patches replay only on a real version
            // transition of an already-catalogued database (see Update_VersionBump_ReplaysPatches_NotFirstContact).
            action.Verify(x => x.ReplayPatches(It.IsAny<SqlDbConnection>(), It.IsAny<IEnumerable<Assembly>>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_VersionBump_ReplaysPatches_NotFirstContact(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            Seed(store, connection, "cat_ctrl_a", "1.0.0", DesiredDto<CtrlA>());
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // Catalogue already populated; the window is (Vc, Vi] = (1.0.0, 2.0.0].
            action.Verify(x => x.ReplayPatches(connection, It.IsAny<IEnumerable<Assembly>>(), Scope, VersionKeyOf("1.0.0"), VersionKeyOf("2.0.0")), Times.Once);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_CleanNoOp_DoesNotReplay(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            Seed(store, connection, "cat_ctrl_a", "1.0.0", DesiredDto<CtrlA>());
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            action.Verify(x => x.ReplayPatches(It.IsAny<SqlDbConnection>(), It.IsAny<IEnumerable<Assembly>>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateTables_DoesNotReplay(string connectionName)
        {
            var connection = Open(connectionName);
            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();

            Controller(action).CreateTables(connection, "1.0.0");

            action.Verify(x => x.ReplayPatches(It.IsAny<SqlDbConnection>(), It.IsAny<IEnumerable<Assembly>>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void DropTables_DoesNotReplay(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            Seed(store, connection, "cat_ctrl_a", "1.0.0", DesiredDto<CtrlA>());
            Seed(store, connection, "cat_ctrl_b", "1.0.0", DesiredDto<CtrlB>());

            var action = new Mock<CatalogEntityController.ICatalogControllerAction>();
            Controller(action).DropTables(connection);

            action.Verify(x => x.ReplayPatches(It.IsAny<SqlDbConnection>(), It.IsAny<IEnumerable<Assembly>>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()), Times.Never);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void IsOrphanScopeExists_DetectsPreCatalogueDatabase(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);
            var ctrl = AdoptController();

            // Nothing exists yet -> greenfield, not orphan.
            ctrl.IsOrphanScopeExists(connection).Should().BeFalse();

            // Tables created before the catalogue (via the old controller) -> orphan.
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            ctrl.IsOrphanScopeExists(connection).Should().BeTrue();

            // After adoption the scope is catalogued -> not orphan.
            ctrl.AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);
            ctrl.IsOrphanScopeExists(connection).Should().BeFalse();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Adopt_TrustModel_Matched_SeedsCatalogue(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);

            // Simulate a pre-catalogue database that already matches the model.
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);

            AdoptController().AdoptExistingScope(connection, "2.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);

            var store = new CatalogStore();
            store.ReadCurrentVersion(connection, AdoptScope).Should().Be("2.0.0");
            var applied = store.ReadAppliedForScope(connection, AdoptScope);
            applied.ContainsKey("cat_adopt_a").Should().BeTrue();
            applied.ContainsKey("cat_adopt_b").Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Adopt_TrustModel_Drifted_Throws(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);

            // Matched database, then drop one table so the DB no longer matches the model.
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            using (var q = connection.GetDropEntityQuery<AdoptB>())
                q.Execute();

            Action act = () => AdoptController().AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);

            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.SchemaUpdateRequired);
            // Catalogue untouched (verification failed before recording).
            new CatalogStore().ReadCurrentVersion(connection, AdoptScope).Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Adopt_ReconcileToModel_AlignsThenSeeds(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);

            // A pre-catalogue database that is missing a table the model declares.
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            using (var q = connection.GetDropEntityQuery<AdoptB>())
                q.Execute();

            AdoptController().AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.ReconcileToModel);

            // The old controller recreated the missing table, and the catalogue is seeded.
            connection.DoesObjectExist("cat_adopt_b", null, "table").Should().BeTrue();
            new CatalogStore().ReadCurrentVersion(connection, AdoptScope).Should().Be("1.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Adopt_AlreadyCatalogued_Throws(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);

            var ctrl = AdoptController();
            ctrl.AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);

            Action act = () => ctrl.AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);
            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.CatalogScopeAlreadyAdopted);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Update_OnPreCatalogueDatabase_RefusesAsOrphan_BeforeAnyDdl(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);

            // Tables exist but the scope has no catalogue (managed before the catalogue).
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);

            Action act = () => AdoptController().UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);

            // The orphan pre-check throws before any DDL and before writing the catalogue.
            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.CatalogOrphanScope);
            new CatalogStore().ReadCurrentVersion(connection, AdoptScope).Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Adopt_TrustModel_IndexDrift_Throws(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptTables(connection);

            // Matched database, then drop the sorted-column index so the DB drifts from the model's indexes.
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            TableDescriptor descriptor = AllEntities.Inst[typeof(AdoptA)].TableDescriptor;
            string sortedColumn = null;
            foreach (TableDescriptor.ColumnInfo c in descriptor)
                if (c.Sorted)
                    sortedColumn = c.Name;
            using (var q = connection.GetQuery(connection.GetDropIndexBuilder(descriptor, sortedColumn)))
                q.ExecuteNoData();

            Action act = () => AdoptController().AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);

            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.SchemaUpdateRequired);
            new CatalogStore().ReadCurrentVersion(connection, AdoptScope).Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Adopt_TrustModel_DynamicPropertiesDrift_Throws(string connectionName)
        {
            var connection = Open(connectionName);
            DropAdoptDpTables(connection);

            // Matched database (owner + EAV side table), then drop the side table so the DB drifts from the
            // model's dynamic-properties declaration.
            new CreateEntityControllerInternal(typeof(CatalogEntityControllerTest).Assembly, AdoptDpScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            TableDescriptor side = AllEntities.Inst[typeof(AdoptDpOwner)].DynamicPropertiesTable;
            using (var q = connection.GetQuery(connection.GetDropTableBuilder(side)))
                q.ExecuteNoData();

            var ctrl = new CatalogEntityController(typeof(CatalogEntityControllerTest).Assembly, AdoptDpScope);
            Action act = () => ctrl.AdoptExistingScope(connection, "1.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);

            act.Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.SchemaUpdateRequired);
            new CatalogStore().ReadCurrentVersion(connection, AdoptDpScope).Should().BeNull();
        }

        private static long CountRows(SqlDbConnection connection)
        {
            var descriptor = AllEntities.Get<EfCatalogRecord>().TableDescriptor;
            var select = connection.GetSelectQueryBuilder(descriptor);
            select.AddToResultset(AggFn.Count);
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                if (!query.ReadNext())
                    return 0;
                return query.GetValue<long>(0);
            }
        }
    }
}

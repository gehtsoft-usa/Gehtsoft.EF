using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Catalog.Store;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.Catalog.Controller
{
    /// <summary>
    /// End-to-end tests for the catalogue's coded-patch (<see cref="IEfPatch"/>) replay, using real patch
    /// classes and the real DDL action on a private in-memory SQLite database (full isolation, like
    /// <c>PatchTest</c>). Asserts the catalogue's own window policy: an empty ledger means "nothing applied
    /// yet" so every patch up to the applied version runs; a populated ledger runs only the open window;
    /// first contact with a pre-existing ledger is refused (adopt-first).
    /// </summary>
    public sealed class CatalogPatchReplayTest
    {
        private const string PatchScope = "cat_patch";

        // Records the versions of the patches that actually ran, in order. Reset at the start of each test;
        // in-memory SQLite plus a per-test connection keeps the tests independent even though this is static
        // (the patch classes are found by reflection, so they cannot capture per-test state directly).
        private static readonly List<string> gApplied = new List<string>();

        [Entity(Scope = PatchScope, Table = "cat_patch_a")]
        public class PatchOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty]
            public int V { get; set; }
        }

        [EfPatch(PatchScope, 1, 0, 0)]
        public class Patch100 : IEfPatch
        {
            public void Apply(SqlDbConnection connection) => gApplied.Add("1.0.0");
        }

        [EfPatch(PatchScope, 1, 5, 0)]
        public class Patch150 : IEfPatch
        {
            public void Apply(SqlDbConnection connection) => gApplied.Add("1.5.0");
        }

        [EfPatch(PatchScope, 2, 0, 0)]
        public class Patch200 : IEfPatch
        {
            public void Apply(SqlDbConnection connection) => gApplied.Add("2.0.0");
        }

        [EfPatch(PatchScope, 3, 0, 0)]
        public class Patch300 : IEfPatch
        {
            public void Apply(SqlDbConnection connection) => gApplied.Add("3.0.0");
        }

        private static CatalogEntityController Controller()
            => new CatalogEntityController(typeof(CatalogPatchReplayTest).Assembly, PatchScope);

        [Fact]
        public void FirstContact_FreshDatabase_RunsNoPatches()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            // A fresh database on first contact runs NO patches: the tables are created directly in the
            // head-2.0.0 state, so there is nothing to migrate. This matches CreateTables and the old
            // EfPatchProcessor, which stamps-and-runs-none on a fresh database. Patches migrate an existing
            // DB across versions; they are not a fresh-install seeding mechanism.
            gApplied.Should().BeEmpty();

            // No patches ran, so the ledger has no rows for the scope.
            connection.GetAllPatches(PatchScope).Count.Should().Be(0);
        }

        [Fact]
        public void IncrementalRun_RunsOnlyTheOpenWindow()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            // Establish the 2.0.0 baseline on a fresh DB - first contact runs no patches (structure is
            // created directly at head 2.0.0).
            controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            gApplied.Should().BeEmpty();

            // Bump to 3.0.0: now it is a real transition of a catalogued DB, so only the patch in
            // (2.0.0, 3.0.0] runs.
            controller.UpdateTables(connection, "3.0.0", EntityUpdateMode.Update);

            gApplied.Should().Equal("3.0.0");
        }

        [Fact]
        public void VersionBumpWithoutNewPatch_RunsNothing_AndDoesNotReapply()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            // Fresh baseline at 2.0.0 (first contact, no patches run).
            controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
            gApplied.Should().BeEmpty();

            // 2.5.0 has no patch of its own; 3.0.0 is above it. The window (2.0.0, 2.5.0] is empty, so
            // nothing runs and already-baked-in patches are not re-run.
            controller.UpdateTables(connection, "2.5.0", EntityUpdateMode.Update);

            gApplied.Should().BeEmpty();
        }

        [Fact]
        public void CreateTables_ThenUpdate_DoesNotRerunPreBakedPatches()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            // Fresh DB created directly at head 2.0.0: tables are already in the post-patch state, and
            // CreateTables runs no patches at all (it just stamps the scope version = 2.0.0).
            controller.CreateTables(connection, "2.0.0");
            gApplied.Should().BeEmpty();

            // A later bump to 3.0.0 must run ONLY (2.0.0, 3.0.0] - never the 1.0.0/1.5.0/2.0.0 patches
            // already baked into the created schema, even though the ledger is empty (the window is keyed
            // on the scope version Vc=2.0.0, not the ledger).
            controller.UpdateTables(connection, "3.0.0", EntityUpdateMode.Update);

            gApplied.Should().Equal("3.0.0");
        }

        [Fact]
        public void AfterAdopt_UpdateRunsOnlyPatchesAboveAdoptedVersion()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            // Simulate a pre-catalogue database: the old controller created the table (no patches, no
            // catalogue). Adopt it at 2.0.0 - the schema is already at head, so TrustModel verifies.
            new CreateEntityControllerInternal(typeof(CatalogPatchReplayTest).Assembly, PatchScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            controller.AdoptExistingScope(connection, "2.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);
            gApplied.Should().BeEmpty();

            // A later update runs only patches above the adopted version - the ≤ 2.0.0 patches are treated
            // as already baked in.
            controller.UpdateTables(connection, "3.0.0", EntityUpdateMode.Update);

            gApplied.Should().Equal("3.0.0");
        }

        [Fact]
        public void FirstContact_WithExistingLedger_IsRefusedAsOrphan()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            // Simulate a database that was patched before the catalogue existed: the ledger has a row for
            // the scope, but the catalogue has no entry yet.
            using (var insert = connection.GetInsertEntityQuery<EfPatchHistoryRecord>())
                insert.Execute(new EfPatchHistoryRecord(PatchScope, 1, 0, 0, DateTime.Now));

            Action act = () => controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CatalogOrphanPatchHistory);
            gApplied.Should().BeEmpty();
            // The orphan pre-check fires before any DDL, so the catalogue is NOT seeded (a re-run would
            // otherwise become a Vi==Vc no-op that masks the orphan forever).
            new CatalogStore().ReadCurrentVersion(connection, PatchScope).Should().BeNull();
        }

        [Fact]
        public void AdoptWithPreExistingLedger_ThenUpdate_RunsOnlyTheWindow()
        {
            gApplied.Clear();
            using var connection = SqliteDbConnectionFactory.CreateMemory();
            var controller = Controller();
            controller.EnsureCatalogInfrastructure(connection);

            // A pre-catalogue database managed by the old controller AND already patched up to 1.5.0 (the
            // real Flow-6 migration shape): tables exist, ledger has rows, no catalogue.
            new CreateEntityControllerInternal(typeof(CatalogPatchReplayTest).Assembly, PatchScope)
                .UpdateTables(connection, EntityUpdateMode.Update);
            using (var insert = connection.GetInsertEntityQuery<EfPatchHistoryRecord>())
            {
                insert.Execute(new EfPatchHistoryRecord(PatchScope, 1, 0, 0, DateTime.Now));
                insert.Execute(new EfPatchHistoryRecord(PatchScope, 1, 5, 0, DateTime.Now));
            }

            // Adopt at 2.0.0 (schema already at head, ledger untouched), then advance to 3.0.0.
            controller.AdoptExistingScope(connection, "2.0.0", CatalogEntityController.CatalogAdoptMode.TrustModel);
            gApplied.Should().BeEmpty();
            controller.UpdateTables(connection, "3.0.0", EntityUpdateMode.Update);

            // Only (2.0.0, 3.0.0] runs: the ledger's 1.0.0/1.5.0 and the baked-in 2.0.0 are all <= Vc and
            // must not re-run.
            gApplied.Should().Equal("3.0.0");
        }
    }
}

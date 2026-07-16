using System;
using System.Threading;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Catalog;
using Gehtsoft.EF.Db.SqlDb.Catalog.Store;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Catalog.Store
{
    /// <summary>
    /// Behavioural tests for <see cref="CatalogStore"/> over every configured live database
    /// (SQLite, MSSQL, Oracle, PostgreSQL, MySQL) via the all-driver fixture. Every read/write goes
    /// through the query builders, matching the store's own no-raw-SQL rule.
    ///
    /// Because <c>ef_catalog</c> is a real, shared table reused across runs, each test first drops it
    /// so it starts from a clean, empty catalogue (row counts and latest-per-table assertions rely on
    /// this). xUnit runs the theories of one class serially, so the per-test reset is race-free; no
    /// other test class touches <c>ef_catalog</c>.
    ///
    /// Covered: bootstrap idempotency; write-read round-trip fidelity; the append-only history rule
    /// (a new row per real change, nothing on a no-op re-run) and reading the latest applied state;
    /// the AppliedUtc timeline; ReadApplied ignoring in-progress (Migrated=false) rows; adopt seeding a
    /// no-op baseline once; and the forward-compat refuse gate on a newer-than-supported snapshot.
    /// </summary>
    public sealed class CatalogStoreTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private static readonly CatalogSerializer Serializer = new CatalogSerializer();

        private readonly SqlConnectionFixtureBase mFixture;

        public CatalogStoreTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        // Returns the shared fixture connection for the driver, first dropping any leftover ef_catalog
        // table so every test starts from an empty catalogue (the connection itself is owned by the
        // fixture and must not be disposed here).
        private SqlDbConnection Open(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            if (connection.DoesObjectExist("ef_catalog", null, "table"))
                using (var drop = connection.GetDropEntityQuery<EfCatalogRecord>())
                    drop.Execute();
            return connection;
        }

        private static CatalogTableDto SampleTable(string name)
        {
            CatalogTableDto dto = new CatalogTableDto { Name = name, Scope = "s" };
            dto.Columns.Add(new CatalogColumnDto { Id = "Id", Name = "id", DbType = "Int32", PrimaryKey = true, Autoincrement = true });
            dto.Columns.Add(new CatalogColumnDto { Id = "Name", Name = "name", DbType = "String", Size = 32, Nullable = true });
            return dto;
        }

        private static string Normalized(CatalogTableDto dto)
            => Serializer.Serialize(new CatalogSnapshot { Table = dto });

        // Reads the latest raw catalogue row via the entity query builder (the store keeps this internal).
        private static EfCatalogRecord ReadLatestRow(SqlDbConnection connection, string scope, string tableName)
        {
            using (var query = connection.GetSelectEntitiesQuery<EfCatalogRecord>())
            {
                query.Where.Property(nameof(EfCatalogRecord.Scope)).Eq(scope ?? string.Empty);
                query.Where.And().Property(nameof(EfCatalogRecord.TableName)).Eq(tableName);
                query.AddOrderBy(nameof(EfCatalogRecord.ID), SortDir.Desc);
                query.Limit = 1;
                return query.ReadOne<EfCatalogRecord>();
            }
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

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void EnsureBootstrapped_IsIdempotent_AndCreatesTable(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();

            store.EnsureBootstrapped(connection);
            connection.DoesObjectExist("ef_catalog", null, "table").Should().BeTrue();

            // A second call must not throw and must leave the table in place.
            store.EnsureBootstrapped(connection);
            connection.DoesObjectExist("ef_catalog", null, "table").Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReadApplied_ReturnsNull_WhenNoEntry(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.ReadApplied(connection, "s", "absent").Should().BeNull();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WriteApplied_Then_ReadApplied_RoundTrips(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            CatalogTableDto written = SampleTable("t");
            store.WriteApplied(connection, "s", "t", "1.0.0", written);

            CatalogTableDto read = store.ReadApplied(connection, "s", "t");
            read.Should().NotBeNull();
            // Deterministic, lossless serialization (Prereq A) makes re-serialized text the equality check.
            Normalized(read).Should().Be(Normalized(written));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WriteApplied_AppendsHistory_OnChange_ReadsLatest(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));

            CatalogTableDto changed = SampleTable("t");
            changed.Columns.Add(new CatalogColumnDto { Id = "Extra", Name = "extra", DbType = "Int32" });
            store.WriteApplied(connection, "s", "t", "2.0.0", changed);

            // A real change appends a new row (history is kept), and ReadApplied returns the latest.
            CountRows(connection).Should().Be(2);
            Normalized(store.ReadApplied(connection, "s", "t")).Should().Be(Normalized(changed));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WriteApplied_NoOpReRun_AppendsNothing(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            // Identical version + identical snapshot => no new row.
            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));

            CountRows(connection).Should().Be(1);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AppliedUtc_StableOnNoOp_NewRowOnVersionBump(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            DateTime first = ReadLatestRow(connection, "s", "t").AppliedUtc;

            // No-op re-run: no new row, so the tip timestamp is preserved exactly.
            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            ReadLatestRow(connection, "s", "t").AppliedUtc.Should().Be(first);

            // Cross a full second so the new row's timestamp is observably later even on
            // second-precision date columns.
            Thread.Sleep(1100);

            store.WriteApplied(connection, "s", "t", "2.0.0", SampleTable("t"));
            EfCatalogRecord tip = ReadLatestRow(connection, "s", "t");
            tip.Version.Should().Be("2.0.0");
            tip.AppliedUtc.Should().BeAfter(first);
            CountRows(connection).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReadApplied_IgnoresInProgressRows(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // The confirmed applied state.
            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));

            // A later, in-progress (Migrated=false) row - the shape a future torn-write two-phase write
            // leaves behind. ReadApplied must skip it and keep returning the last confirmed state.
            CatalogTableDto inProgress = SampleTable("t");
            inProgress.Columns.Add(new CatalogColumnDto { Id = "Extra", Name = "extra", DbType = "Int32" });
            using (var insert = connection.GetInsertEntityQuery<EfCatalogRecord>())
                insert.Execute(new EfCatalogRecord
                {
                    Scope = "s",
                    TableName = "t",
                    Version = "2.0.0",
                    AppliedUtc = DateTime.UtcNow,
                    Migrated = false,
                    SchemaFormatVersion = Serializer.CurrentSchemaFormatVersion,
                    Snapshot = Normalized(inProgress),
                });

            Normalized(store.ReadApplied(connection, "s", "t")).Should().Be(Normalized(SampleTable("t")));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReadAppliedForScope_ReturnsLatestPerTable_InOneScope(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Two tables in scope "s"; table "t1" has history (v1 then v2).
            store.WriteApplied(connection, "s", "t1", "1.0.0", SampleTable("t1"));
            CatalogTableDto t1v2 = SampleTable("t1");
            t1v2.Columns.Add(new CatalogColumnDto { Id = "Extra", Name = "extra", DbType = "Int32" });
            store.WriteApplied(connection, "s", "t1", "2.0.0", t1v2);
            store.WriteApplied(connection, "s", "t2", "1.0.0", SampleTable("t2"));

            // A different scope must not leak into the result.
            store.WriteApplied(connection, "other", "t3", "1.0.0", SampleTable("t3"));

            var map = store.ReadAppliedForScope(connection, "s");

            map.Count.Should().Be(2);
            map.ContainsKey("t3").Should().BeFalse();
            Normalized(map["t1"]).Should().Be(Normalized(t1v2));   // latest, not v1
            Normalized(map["t2"]).Should().Be(Normalized(SampleTable("t2")));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReadAppliedForScope_IgnoresInProgressRows(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));

            CatalogTableDto inProgress = SampleTable("t");
            inProgress.Columns.Add(new CatalogColumnDto { Id = "Extra", Name = "extra", DbType = "Int32" });
            using (var insert = connection.GetInsertEntityQuery<EfCatalogRecord>())
                insert.Execute(new EfCatalogRecord
                {
                    Scope = "s",
                    TableName = "t",
                    Version = "2.0.0",
                    AppliedUtc = DateTime.UtcNow,
                    Migrated = false,
                    SchemaFormatVersion = Serializer.CurrentSchemaFormatVersion,
                    Snapshot = Normalized(inProgress),
                });

            var map = store.ReadAppliedForScope(connection, "s");
            Normalized(map["t"]).Should().Be(Normalized(SampleTable("t")));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReadAppliedForScope_ReturnsEmpty_WhenScopeUncatalogued(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.ReadAppliedForScope(connection, "s").Count.Should().Be(0);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReadApplied_Refuses_NewerThanSupportedSnapshot(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            int tooNew = Serializer.CurrentSchemaFormatVersion + 100;
            // A hand-written blob claiming a newer schema format - the exact forward-compat trap.
            string blob = "{\"schemaFormatVersion\":" + tooNew + ",\"table\":{\"name\":\"t\",\"scope\":\"s\",\"columns\":[]}}";

            using (var insert = connection.GetInsertEntityQuery<EfCatalogRecord>())
                insert.Execute(new EfCatalogRecord
                {
                    Scope = "s",
                    TableName = "t",
                    Version = "5.0.0",
                    AppliedUtc = DateTime.UtcNow,
                    Migrated = true,
                    SchemaFormatVersion = tooNew,
                    Snapshot = blob,
                });

            Action act = () => store.ReadApplied(connection, "s", "t");
            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CatalogFormatTooNew);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AdvanceVersion_BumpsLiveRowInPlace_WithoutNewRowOrSnapshotChange(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            string snapshotBefore = ReadLatestRow(connection, "s", "t").Snapshot;

            store.AdvanceVersion(connection, "s", "t", "2.0.0");

            // The version moves forward on the same row: no new history row, snapshot untouched.
            CountRows(connection).Should().Be(1);
            EfCatalogRecord tip = ReadLatestRow(connection, "s", "t");
            tip.Version.Should().Be("2.0.0");
            tip.Snapshot.Should().Be(snapshotBefore);
            tip.Dropped.Should().BeFalse();
            Normalized(store.ReadApplied(connection, "s", "t")).Should().Be(Normalized(SampleTable("t")));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AdvanceVersion_IsNoOp_WhenAbsentOrSameVersion(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Nothing to advance for a table with no entry.
            store.AdvanceVersion(connection, "s", "absent", "2.0.0");
            CountRows(connection).Should().Be(0);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            // Already at this version: no write.
            store.AdvanceVersion(connection, "s", "t", "1.0.0");
            CountRows(connection).Should().Be(1);
            ReadLatestRow(connection, "s", "t").Version.Should().Be("1.0.0");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void LiveVersion_IsUniform_AfterMixedChangedAndUnchangedRun(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Two tables applied at 1.0.0.
            store.WriteApplied(connection, "s", "t1", "1.0.0", SampleTable("t1"));
            store.WriteApplied(connection, "s", "t2", "1.0.0", SampleTable("t2"));

            // A run at 2.0.0: t1 changes (new row), t2 is unchanged (version advanced in place).
            CatalogTableDto t1v2 = SampleTable("t1");
            t1v2.Columns.Add(new CatalogColumnDto { Id = "Extra", Name = "extra", DbType = "Int32" });
            store.WriteApplied(connection, "s", "t1", "2.0.0", t1v2);
            store.AdvanceVersion(connection, "s", "t2", "2.0.0");

            // Both live rows now carry the same current version (the uniform-version invariant).
            ReadLatestRow(connection, "s", "t1").Version.Should().Be("2.0.0");
            ReadLatestRow(connection, "s", "t2").Version.Should().Be("2.0.0");
            // History kept for the changed table (2 rows); the unchanged table stayed a single row.
            CountRows(connection).Should().Be(3);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WriteTombstone_MakesTableReadAsAbsent_ButKeepsHistory(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            store.WriteTombstone(connection, "s", "t", "2.0.0");

            // A dropped table reads as absent through both read paths...
            store.ReadApplied(connection, "s", "t").Should().BeNull();
            store.ReadAppliedForScope(connection, "s").ContainsKey("t").Should().BeFalse();
            // ...but the tombstone is a real appended row that keeps the last-known snapshot for audit.
            CountRows(connection).Should().Be(2);
            EfCatalogRecord tip = ReadLatestRow(connection, "s", "t");
            tip.Dropped.Should().BeTrue();
            tip.Version.Should().Be("2.0.0");
            tip.Snapshot.Should().Be(Normalized(SampleTable("t")));
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void WriteTombstone_IsNoOp_WhenAlreadyDroppedOrAbsent(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            // Nothing to tombstone for a table with no entry.
            store.WriteTombstone(connection, "s", "absent", "2.0.0");
            CountRows(connection).Should().Be(0);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            store.WriteTombstone(connection, "s", "t", "2.0.0");
            // A second tombstone on the same table appends nothing.
            store.WriteTombstone(connection, "s", "t", "3.0.0");
            CountRows(connection).Should().Be(2);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void Recreate_AfterTombstone_AppendsNewLiveRow(string connectionName)
        {
            var connection = Open(connectionName);
            var store = new CatalogStore();
            store.EnsureBootstrapped(connection);

            store.WriteApplied(connection, "s", "t", "1.0.0", SampleTable("t"));
            store.WriteTombstone(connection, "s", "t", "2.0.0");

            // Reintroducing the table writes a fresh live row after the tombstone (not swallowed as a no-op).
            CatalogTableDto recreated = SampleTable("t");
            recreated.Columns.Add(new CatalogColumnDto { Id = "Extra", Name = "extra", DbType = "Int32" });
            store.WriteApplied(connection, "s", "t", "3.0.0", recreated);

            Normalized(store.ReadApplied(connection, "s", "t")).Should().Be(Normalized(recreated));
            CountRows(connection).Should().Be(3);
            ReadLatestRow(connection, "s", "t").Dropped.Should().BeFalse();
        }
    }
}

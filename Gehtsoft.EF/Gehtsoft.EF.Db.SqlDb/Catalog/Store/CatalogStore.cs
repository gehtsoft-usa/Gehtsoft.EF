using System;
using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.Catalog.Store
{
    /// <summary>
    /// The persistence layer of the schema catalogue: reads and writes the applied-schema snapshot for
    /// each <c>(scope, table)</c> pair through the <see cref="EfCatalogRecord"/> entity, i.e. via the
    /// ordinary query builders - no hand-written SQL.
    ///
    /// The catalogue is an append-only history of snapshots with a single mutable "current pointer" per
    /// table: <see cref="WriteApplied"/> appends a new row when the snapshot changes; <see cref="AdvanceVersion"/>
    /// bumps an unchanged table's live row version in place (no new row); <see cref="WriteTombstone"/>
    /// appends a <see cref="EfCatalogRecord.Dropped"/> row when a table is dropped. Superseded rows are
    /// never mutated, so the history (audit / diff-any-two-versions) is intact; only the latest live row
    /// moves forward. The <see cref="EfCatalogRecord.Version"/> carried by the live rows is therefore the
    /// "last version at which the table has this descriptor", kept uniform across the scope by the
    /// controller. <see cref="ReadApplied"/> returns the latest applied
    /// (<see cref="EfCatalogRecord.Migrated"/> = `true`, non-tombstone) snapshot. Torn-write recovery over
    /// this shape is a later phase (v1 always records <see cref="EfCatalogRecord.Migrated"/> = `true`).
    ///
    /// The store is authoritative once populated. On first contact with a table there is no entry, so
    /// <see cref="ReadApplied"/> returns `null` and the controller diffs the model against `null`
    /// (a full CreateTable) - v1 is greenfield: the catalogue is born with the database. Migrating an
    /// existing, mismatched database onto the catalogue needs the actual DB state, which only
    /// compare-with-actual (a later phase) can supply; there is deliberately no trust-the-model "adopt"
    /// seed, because when the switch to catalogues ships alongside real model changes it would record
    /// the new model as already-applied and silently skip that release's DDL. The store enforces the
    /// forward-compatibility refuse gate: a snapshot written by a more capable framework build (a higher
    /// <see cref="CatalogSerializer.CurrentSchemaFormatVersion"/>) makes the store throw
    /// <see cref="EfExceptionCode.CatalogFormatTooNew"/> rather than let an older build act on it.
    ///
    /// The store does NOT acquire the instance lock; the controller holds
    /// <see cref="InstanceLock.IDbInstanceLock"/> across the whole read-diff-apply and the store runs
    /// inside that critical section.
    /// </summary>
    internal sealed class CatalogStore
    {
        private readonly CatalogSerializer mSerializer = new CatalogSerializer();

        // The catalogued table's scope is normalized so the lookup column is never null and can be
        // matched with a plain equality on every driver.
        private static string NormalizeScope(string scope) => scope ?? string.Empty;

        private static TableDescriptor CatalogTable => AllEntities.Get<EfCatalogRecord>().TableDescriptor;

        /// <summary>
        /// Creates the <c>ef_catalog</c> table if it is not present yet. Idempotent, and tolerant of a
        /// concurrent creator racing between the existence check and the create.
        /// </summary>
        /// <param name="connection">The connection to bootstrap on.</param>
        public void EnsureBootstrapped(SqlDbConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            string tableName = CatalogTable.Name;
            if (connection.DoesObjectExist(tableName, null, "table"))
                return;

            try
            {
                using (var query = connection.GetCreateEntityQuery<EfCatalogRecord>())
                    query.Execute();
            }
            catch
            {
                // Another process may have created the table between the check and the create; tolerate
                // that race but surface a genuine failure.
                if (!connection.DoesObjectExist(tableName, null, "table"))
                    throw;
            }
        }

        /// <summary>
        /// Reads the current applied schema snapshot for a table - the latest
        /// <see cref="EfCatalogRecord.Migrated"/> row - or `null` when the table has no catalogue entry
        /// yet (first contact) <b>or its latest row is a <see cref="EfCatalogRecord.Dropped"/> tombstone</b>
        /// (the table was dropped and is currently absent).
        /// </summary>
        /// <param name="connection">The connection to read from.</param>
        /// <param name="scope">The catalogued table's scope (`null` means "no scope").</param>
        /// <param name="tableName">The catalogued table's name.</param>
        /// <exception cref="EfSqlException">
        /// <see cref="EfExceptionCode.CatalogFormatTooNew"/> when the stored snapshot was written by a
        /// framework build newer than this one - no DDL must follow.
        /// </exception>
        public CatalogTableDto ReadApplied(SqlDbConnection connection, string scope, string tableName)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));

            EfCatalogRecord row = ReadLatestRow(connection, scope, tableName, migratedOnly: true);
            if (row == null)
                return null;

            // Enforce the refuse gate even on a tombstone (an older build must not act on a newer
            // catalogue's timeline), then report a dropped table as absent.
            CatalogSnapshot snapshot = mSerializer.Deserialize(row.Snapshot);
            if (snapshot.IsNewerThanSupported)
                throw new EfSqlException(EfExceptionCode.CatalogFormatTooNew,
                    tableName, snapshot.SchemaFormatVersion, mSerializer.CurrentSchemaFormatVersion);
            if (row.Dropped)
                return null;
            return snapshot.Table;
        }

        /// <summary>
        /// Reads every table's current applied snapshot for a whole scope in a <b>single query</b>,
        /// returning a <c>tableName → snapshot</c> map (the latest <see cref="EfCatalogRecord.Migrated"/>
        /// row per table). This is the controller's batch read: it lets `UpdateTables` load the scope's
        /// catalogue once instead of issuing a select per table. Tables with no entry - and tables whose
        /// latest row is a <see cref="EfCatalogRecord.Dropped"/> tombstone - are simply absent from the
        /// map (first contact / dropped).
        /// </summary>
        /// <param name="connection">The connection to read from.</param>
        /// <param name="scope">The scope to load (`null` means "no scope").</param>
        /// <exception cref="EfSqlException">
        /// <see cref="EfExceptionCode.CatalogFormatTooNew"/> when any entry was written by a framework
        /// build newer than this one - no DDL must follow.
        /// </exception>
        public IReadOnlyDictionary<string, CatalogTableDto> ReadAppliedForScope(SqlDbConnection connection, string scope)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            // Read the scope's confirmed rows oldest-first and keep the last (highest id) seen per table,
            // so each table maps to its latest applied state after one pass.
            Dictionary<string, EfCatalogRecord> latestByTable = new Dictionary<string, EfCatalogRecord>(StringComparer.Ordinal);
            using (var query = connection.GetSelectEntitiesQuery<EfCatalogRecord>())
            {
                query.Where.Property(nameof(EfCatalogRecord.Scope)).Eq(NormalizeScope(scope));
                query.Where.And().Property(nameof(EfCatalogRecord.Migrated)).Eq(true);
                query.AddOrderBy(nameof(EfCatalogRecord.ID), SortDir.Asc);
                foreach (EfCatalogRecord row in query.ReadAll<EfCatalogRecord>())
                    latestByTable[row.TableName] = row;
            }

            Dictionary<string, CatalogTableDto> result = new Dictionary<string, CatalogTableDto>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, EfCatalogRecord> entry in latestByTable)
            {
                // Refuse gate applies to every latest row (tombstones included); a dropped table is then
                // left out of the map so the controller sees it as absent.
                CatalogSnapshot snapshot = mSerializer.Deserialize(entry.Value.Snapshot);
                if (snapshot.IsNewerThanSupported)
                    throw new EfSqlException(EfExceptionCode.CatalogFormatTooNew,
                        entry.Key, snapshot.SchemaFormatVersion, mSerializer.CurrentSchemaFormatVersion);
                if (entry.Value.Dropped)
                    continue;
                result[entry.Key] = snapshot.Table;
            }
            return result;
        }

        /// <summary>
        /// Appends a new applied-schema row for a table when its recorded state changes. A re-apply of
        /// the identical state (same version and byte-identical snapshot as the latest row) is a no-op
        /// and appends nothing, so the history grows only with real changes and timestamps stay stable.
        /// </summary>
        /// <param name="connection">The connection to write to.</param>
        /// <param name="scope">The catalogued table's scope (`null` means "no scope").</param>
        /// <param name="tableName">The catalogued table's name.</param>
        /// <param name="version">The DB version being applied.</param>
        /// <param name="table">The applied schema to record.</param>
        /// <exception cref="EfSqlException">
        /// <see cref="EfExceptionCode.CatalogFormatTooNew"/> when an existing entry was written by a
        /// newer framework build (an older build must never write over a newer catalogue's timeline).
        /// </exception>
        public void WriteApplied(SqlDbConnection connection, string scope, string tableName, string version, CatalogTableDto table)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));
            if (table == null)
                throw new ArgumentNullException(nameof(table));

            int formatVersion = mSerializer.CurrentSchemaFormatVersion;
            string snapshotText = mSerializer.Serialize(new CatalogSnapshot { Table = table });

            // Any row (migrated or not) written by a newer build blocks an older build from appending.
            EfCatalogRecord latest = ReadLatestRow(connection, scope, tableName, migratedOnly: false);
            if (latest != null)
            {
                if (latest.SchemaFormatVersion > formatVersion)
                    throw new EfSqlException(EfExceptionCode.CatalogFormatTooNew,
                        tableName, latest.SchemaFormatVersion, formatVersion);

                // True no-op: identical version and identical snapshot as the current tip => append
                // nothing. A tombstone tip never counts as a no-op, so reintroducing a table always
                // appends a fresh live row (recreate) rather than being swallowed.
                if (latest.Migrated && !latest.Dropped &&
                    string.Equals(latest.Version, version, StringComparison.Ordinal) &&
                    string.Equals(latest.Snapshot, snapshotText, StringComparison.Ordinal))
                    return;
            }

            EfCatalogRecord row = new EfCatalogRecord
            {
                Scope = NormalizeScope(scope),
                TableName = tableName,
                Version = version,
                AppliedUtc = DateTime.UtcNow,
                Migrated = true,
                Dropped = false,
                SchemaFormatVersion = formatVersion,
                Snapshot = snapshotText,
            };
            using (var query = connection.GetInsertEntityQuery<EfCatalogRecord>())
                query.Execute(row);
        }

        /// <summary>
        /// Advances an unchanged table's version in place: sets the <b>latest live row</b>'s
        /// <see cref="EfCatalogRecord.Version"/> to <paramref name="version"/> without appending a row or
        /// touching its snapshot. This keeps every live table stamped with the current DB version (the
        /// "last version at which the table has this descriptor" semantic) while the append-only history
        /// keeps a row only when a snapshot actually changes. No-op when the table has no entry, when its
        /// latest row is a tombstone, or when it is already at <paramref name="version"/>.
        /// </summary>
        /// <param name="connection">The connection to write to.</param>
        /// <param name="scope">The catalogued table's scope (`null` means "no scope").</param>
        /// <param name="tableName">The catalogued table's name.</param>
        /// <param name="version">The DB version to stamp onto the live row.</param>
        /// <exception cref="EfSqlException">
        /// <see cref="EfExceptionCode.CatalogFormatTooNew"/> when the live row was written by a newer
        /// framework build (an older build must not rewrite a newer catalogue's timeline).
        /// </exception>
        public void AdvanceVersion(SqlDbConnection connection, string scope, string tableName, string version)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));

            EfCatalogRecord latest = ReadLatestRow(connection, scope, tableName, migratedOnly: true);
            if (latest == null || latest.Dropped)
                return;
            if (latest.SchemaFormatVersion > mSerializer.CurrentSchemaFormatVersion)
                throw new EfSqlException(EfExceptionCode.CatalogFormatTooNew,
                    tableName, latest.SchemaFormatVersion, mSerializer.CurrentSchemaFormatVersion);
            if (string.Equals(latest.Version, version, StringComparison.Ordinal))
                return;

            latest.Version = version;
            using (var query = connection.GetUpdateEntityQuery<EfCatalogRecord>())
                query.Execute(latest);
        }

        /// <summary>
        /// Records that a table was dropped: appends a <see cref="EfCatalogRecord.Dropped"/> tombstone row
        /// stamped with <paramref name="version"/>, carrying the last-known snapshot for audit. After this
        /// the table reads as absent (see <see cref="ReadApplied"/>). No-op when the table has no entry or
        /// its latest row is already a tombstone (so a re-run does not pile up tombstones).
        /// </summary>
        /// <param name="connection">The connection to write to.</param>
        /// <param name="scope">The catalogued table's scope (`null` means "no scope").</param>
        /// <param name="tableName">The catalogued table's name.</param>
        /// <param name="version">The DB version at which the table was dropped.</param>
        /// <exception cref="EfSqlException">
        /// <see cref="EfExceptionCode.CatalogFormatTooNew"/> when the live row was written by a newer
        /// framework build.
        /// </exception>
        public void WriteTombstone(SqlDbConnection connection, string scope, string tableName, string version)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (tableName == null)
                throw new ArgumentNullException(nameof(tableName));

            EfCatalogRecord latest = ReadLatestRow(connection, scope, tableName, migratedOnly: true);
            if (latest == null || latest.Dropped)
                return;
            if (latest.SchemaFormatVersion > mSerializer.CurrentSchemaFormatVersion)
                throw new EfSqlException(EfExceptionCode.CatalogFormatTooNew,
                    tableName, latest.SchemaFormatVersion, mSerializer.CurrentSchemaFormatVersion);

            EfCatalogRecord row = new EfCatalogRecord
            {
                Scope = NormalizeScope(scope),
                TableName = tableName,
                Version = version,
                AppliedUtc = DateTime.UtcNow,
                Migrated = true,
                Dropped = true,
                SchemaFormatVersion = latest.SchemaFormatVersion,
                Snapshot = latest.Snapshot,
            };
            using (var query = connection.GetInsertEntityQuery<EfCatalogRecord>())
                query.Execute(row);
        }

        /// <summary>
        /// Returns the scope's current DB version - the <see cref="EfCatalogRecord.Version"/> of the
        /// most recently written live (<see cref="EfCatalogRecord.Migrated"/> = `true`) row - or `null`
        /// when the scope has no entries yet (first contact). Because every run stamps every live table
        /// with the run's version (changed tables via a new row, unchanged via
        /// <see cref="AdvanceVersion"/>), the highest-id live row always carries the current version.
        /// </summary>
        /// <param name="connection">The connection to read from.</param>
        /// <param name="scope">The scope to inspect (`null` means "no scope").</param>
        public string ReadCurrentVersion(SqlDbConnection connection, string scope)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (var query = connection.GetSelectEntitiesQuery<EfCatalogRecord>())
            {
                query.Where.Property(nameof(EfCatalogRecord.Scope)).Eq(NormalizeScope(scope));
                query.Where.And().Property(nameof(EfCatalogRecord.Migrated)).Eq(true);
                query.AddOrderBy(nameof(EfCatalogRecord.ID), SortDir.Desc);
                query.Limit = 1;
                EfCatalogRecord row = query.ReadOne<EfCatalogRecord>();
                return row?.Version;
            }
        }

        // The latest row for a (scope, table) pair by insertion order, or `null` when there is none.
        // When migratedOnly is set, in-progress (Migrated=false) rows are skipped - so ReadApplied never
        // returns a state whose DDL has not been confirmed applied.
        private static EfCatalogRecord ReadLatestRow(SqlDbConnection connection, string scope, string tableName, bool migratedOnly)
        {
            using (var query = connection.GetSelectEntitiesQuery<EfCatalogRecord>())
            {
                query.Where.Property(nameof(EfCatalogRecord.Scope)).Eq(NormalizeScope(scope));
                query.Where.And().Property(nameof(EfCatalogRecord.TableName)).Eq(tableName);
                if (migratedOnly)
                    query.Where.And().Property(nameof(EfCatalogRecord.Migrated)).Eq(true);
                query.AddOrderBy(nameof(EfCatalogRecord.ID), SortDir.Desc);
                query.Limit = 1;
                return query.ReadOne<EfCatalogRecord>();
            }
        }
    }
}

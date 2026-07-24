using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading;
using Gehtsoft.EF.Db.SqlDb.InstanceLock;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb
{
    public abstract partial class SqlDbConnection
    {
        /// <summary>
        /// The name of the self-bootstrapping table that backs the portable instance-lock lease
        /// fallback.
        /// </summary>
        internal const string InstanceLockTableName = "ef_catalog_lock";

        /// <summary>
        /// <para>The default lease duration used by the instance-lock acquire methods when the caller does not pass one.</para>
        /// <para>Deliberately modest - size it to the caller's longest realistic critical section by passing
        /// an explicit lease.</para>
        /// </summary>
        public static readonly TimeSpan DefaultInstanceLockLease = TimeSpan.FromSeconds(30);

        // Poll interval of the acquire retry loop (client-side wait; the lease clock is the server's).
        private const int InstanceLockPollMs = 50;

        private readonly object mInstanceLockSync = new object();
        private HashSet<string> mHeldInstanceLocks;

        // The fixed lease-table schema, built once and shared by the lock's query builders:
        // name (PK) | owner (opaque token) | expires_at (server LinuxSeconds when the lease lapses).
        private static readonly TableDescriptor mInstanceLockTable = BuildInstanceLockTable();

        private static TableDescriptor BuildInstanceLockTable()
        {
            TableDescriptor descriptor = new TableDescriptor(InstanceLockTableName);
            descriptor.Add(new TableDescriptor.ColumnInfo() { ID = "Name", Name = "name", DbType = DbType.String, Size = 128, PrimaryKey = true, Nullable = false });
            descriptor.Add(new TableDescriptor.ColumnInfo() { ID = "Owner", Name = "owner", DbType = DbType.String, Size = 128, Nullable = true });
            descriptor.Add(new TableDescriptor.ColumnInfo() { ID = "ExpiresAt", Name = "expires_at", DbType = DbType.Int64, Nullable = false });
            return descriptor;
        }

        private static TableDescriptor.ColumnInfo InstanceLockNameColumn => mInstanceLockTable["Name"];
        private static TableDescriptor.ColumnInfo InstanceLockOwnerColumn => mInstanceLockTable["Owner"];
        private static TableDescriptor.ColumnInfo InstanceLockExpiresColumn => mInstanceLockTable["ExpiresAt"];

        /// <summary>
        /// <para>Acquires a database-instance-wide advisory lock, blocking until it is obtained or the timeout elapses.</para>
        /// <para>Two processes that share the same database and acquire the same name serialize; hold the
        /// returned handle for the whole critical section and dispose it to release. The lock is not
        /// reentrant: acquiring a name this connection already holds throws InvalidOperationException
        /// immediately (fast-fail, not a deadlock).</para>
        /// </summary>
        /// <param name="name">The lock name (the resource to serialize on).</param>
        /// <param name="timeout">How long to wait for the lock before giving up.</param>
        /// <param name="leaseDuration">
        /// For the lease fallback, how long the lock survives without release before another process may
        /// reclaim it (crash safety). Defaults to DefaultInstanceLockLease. Ignored by native advisory locks
        /// that auto-release when the session drops.
        /// </param>
        /// <returns>The lock handle. Dispose it to release.</returns>
        /// <exception cref="EfSqlException">
        /// Thrown with code LockTimeout if the lock could not be acquired within the timeout.
        /// </exception>
        public IDbInstanceLock AcquireInstanceLock(string name, TimeSpan timeout, TimeSpan? leaseDuration = null)
        {
            if (!TryAcquireInstanceLock(name, timeout, out IDbInstanceLock handle, leaseDuration))
                throw new EfSqlException(EfExceptionCode.LockTimeout, name);
            return handle;
        }

        /// <summary>
        /// <para>Attempts to acquire a database-instance-wide advisory lock without throwing on contention.</para>
        /// <para>Same semantics as AcquireInstanceLock but returns false instead of throwing when the timeout
        /// elapses.</para>
        /// </summary>
        /// <param name="name">The lock name (the resource to serialize on).</param>
        /// <param name="timeout">How long to wait for the lock before giving up.</param>
        /// <param name="handle">The acquired handle on success; null on timeout.</param>
        /// <param name="leaseDuration">See AcquireInstanceLock.</param>
        /// <returns>true if the lock was acquired; false if it timed out.</returns>
        public bool TryAcquireInstanceLock(string name, TimeSpan timeout, out IDbInstanceLock handle, TimeSpan? leaseDuration = null)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            handle = null;

            // Reserve the name up front so a re-acquire (nested or from another thread on this same
            // connection) fails fast instead of deadlocking on a lock it already holds.
            lock (mInstanceLockSync)
            {
                if (mHeldInstanceLocks == null)
                    mHeldInstanceLocks = new HashSet<string>(StringComparer.Ordinal);
                if (mHeldInstanceLocks.Contains(name))
                    throw new InvalidOperationException(
                        $"The instance lock '{name}' is already held by this connection (instance locks are not reentrant).");
                mHeldInstanceLocks.Add(name);
            }

            IDbInstanceLock acquired;
            try
            {
                acquired = AcquireInstanceLockCore(name, timeout, leaseDuration ?? DefaultInstanceLockLease);
            }
            catch
            {
                UnregisterInstanceLock(name);
                throw;
            }

            if (acquired == null)
            {
                UnregisterInstanceLock(name);
                return false;
            }

            handle = acquired;
            return true;
        }

        // Removes a name from the held set. Called on acquire failure and from a handle's Dispose.
        internal void UnregisterInstanceLock(string name)
        {
            lock (mInstanceLockSync)
                mHeldInstanceLocks?.Remove(name);
        }

        /// <summary>
        /// <para>Acquires the lock, returning the handle on success or null on timeout.</para>
        /// <para>The base implementation is the portable lease fallback backed by the [c]ef_catalog_lock[/c]
        /// table; a driver overrides it with its native session-scoped advisory lock. The reentrancy guard
        /// and the held-name bookkeeping live in the public methods, so an override only has to acquire and
        /// return a handle whose Dispose calls the connection's UnregisterInstanceLock helper.</para>
        /// </summary>
        /// <param name="name">The lock name.</param>
        /// <param name="timeout">How long to wait before giving up.</param>
        /// <param name="lease">The lease duration for the fallback.</param>
        protected virtual IDbInstanceLock AcquireInstanceLockCore(string name, TimeSpan timeout, TimeSpan lease)
        {
            EnsureInstanceLockTable();
            EnsureInstanceLockRow(name);

            string owner = Guid.NewGuid().ToString("N");
            long leaseSeconds = (long)Math.Ceiling(lease.TotalSeconds);
            if (leaseSeconds < 1)
                leaseSeconds = 1;

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (true)
            {
                if (TryClaimInstanceLease(name, owner, leaseSeconds))
                    return new LeaseInstanceLock(this, name, owner);

                TimeSpan left = timeout - stopwatch.Elapsed;
                if (left <= TimeSpan.Zero)
                    return null;

                int waitMs = InstanceLockPollMs;
                if (left.TotalMilliseconds < waitMs)
                    waitMs = (int)Math.Max(1, left.TotalMilliseconds);
                Thread.Sleep(waitMs);
            }
        }

        // Self-bootstraps the lease table via the create-table builder.
        private void EnsureInstanceLockTable()
        {
            if (DoesObjectExist(InstanceLockTableName, null, "table"))
                return;

            try
            {
                using (var query = GetQuery(GetCreateTableBuilder(mInstanceLockTable)))
                    query.ExecuteNoData();
            }
            catch
            {
                // Another process may have created the table between the check and the CREATE;
                // tolerate that race but surface a genuine failure.
                if (!DoesObjectExist(InstanceLockTableName, null, "table"))
                    throw;
            }
        }

        // Seeds a claimable (owner NULL, already-expired) row for the name if none exists yet.
        private void EnsureInstanceLockRow(string name)
        {
            if (CountInstanceLockRows(name) != 0)
                return;

            try
            {
                InsertQueryBuilder insert = GetInsertQueryBuilder(mInstanceLockTable);
                insert.ReturnAutoincrement = false;
                using (var query = GetQuery(insert))
                {
                    query.BindParam("name", name);
                    query.BindNull("owner", DbType.String);
                    query.BindParam("expires_at", (long)0);
                    query.ExecuteNoData();
                }
            }
            catch
            {
                // A concurrent seeder may have inserted the row first; swallow only that race.
                if (CountInstanceLockRows(name) == 0)
                    throw;
            }
        }

        private long CountInstanceLockRows(string name)
        {
            SelectQueryBuilder select = GetSelectQueryBuilder(mInstanceLockTable);
            select.AddToResultset(AggFn.Count);
            select.Where.Property(InstanceLockNameColumn).Eq().Parameter("name");
            using (var query = GetQuery(select))
            {
                query.BindParam("name", name);
                query.ExecuteReader();
                if (!query.ReadNext())
                    return 0;
                return query.GetValue<long>(0);
            }
        }

        // The single atomic, server-clocked claim: succeeds iff the row is free or its lease expired.
        // "now" is the server's LinuxSeconds, so expiry is computed and compared on the server clock.
        private bool TryClaimInstanceLease(string name, string owner, long leaseSeconds)
        {
            string now = GetLanguageSpecifics().GetSqlFunction(SqlFunctionId.LinuxSeconds, null);
            if (string.IsNullOrEmpty(now))
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported);

            UpdateQueryBuilder update = GetUpdateQueryBuilder(mInstanceLockTable);
            // Every raw operand here is framework-generated (the dialect's LinuxSeconds rendering),
            // so trust the whole builder rather than the caller's text.
            update.SuppressScalarProtection = true;
            update.AddUpdateColumn(InstanceLockOwnerColumn, "owner");
            update.AddUpdateColumnExpression(InstanceLockExpiresColumn, $"{now} + @lease");
            update.Where.Property(InstanceLockNameColumn).Eq().Parameter("name");
            update.Where.And(group =>
            {
                group.Property(InstanceLockOwnerColumn).IsNull();
                group.Or().Property(InstanceLockExpiresColumn).Ls().Raw(now);
            });

            using (var query = GetQuery(update))
            {
                query.BindParam("owner", owner);
                query.BindParam("lease", leaseSeconds);
                query.BindParam("name", name);
                return query.ExecuteNoData() > 0;
            }
        }

        // Releases a lease this connection holds; clears the token only if it is still ours.
        internal void ReleaseInstanceLockLease(string name, string owner)
        {
            UpdateQueryBuilder update = GetUpdateQueryBuilder(mInstanceLockTable);
            update.AddUpdateColumnExpression(InstanceLockOwnerColumn, "NULL");
            update.AddUpdateColumnExpression(InstanceLockExpiresColumn, "0");
            update.Where.Property(InstanceLockNameColumn).Eq().Parameter("name");
            update.Where.And().Property(InstanceLockOwnerColumn).Eq().Parameter("owner");

            using (var query = GetQuery(update))
            {
                query.BindParam("name", name);
                query.BindParam("owner", owner);
                query.ExecuteNoData();
            }
        }
    }
}

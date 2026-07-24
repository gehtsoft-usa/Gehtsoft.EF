using System;
using System.IO;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.InstanceLock;
using Gehtsoft.EF.Db.SqliteDb;
using Xunit;

namespace Gehtsoft.EF.Test.InstanceLock
{
    /// <summary>
    /// Verifies the portable lease fallback (<c>ef_catalog_lock</c>) that backs
    /// <see cref="SqlDbConnection.AcquireInstanceLock(string, System.TimeSpan, System.TimeSpan?)"/>.
    /// SQLite is the always-available driver here; the native per-driver advisory locks are exercised
    /// against live databases elsewhere.
    ///
    /// The tests use a shared file database (not <c>:memory:</c>, which is private per connection) so
    /// two connections contend on the same lease table.
    /// </summary>
    public sealed class LeaseInstanceLockTest : IDisposable
    {
        private const string LockName = "ef_catalog";
        private static readonly TimeSpan Immediate = TimeSpan.Zero;
        private static readonly TimeSpan ShortWait = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan LongLease = TimeSpan.FromMinutes(5);

        private readonly string mFile;

        public LeaseInstanceLockTest()
        {
            mFile = Path.Combine(Path.GetTempPath(), "ef_lease_" + Guid.NewGuid().ToString("N") + ".sqlite");
        }

        public void Dispose()
        {
            if (File.Exists(mFile))
            {
                try { File.Delete(mFile); } catch { /* best-effort cleanup */ }
            }
        }

        private SqlDbConnection Open() => SqliteDbConnectionFactory.CreateFile(mFile, false);

        private string ReadOwner(SqlDbConnection connection, string name)
        {
            using var query = connection.GetQuery(
                $"SELECT owner FROM {SqlDbConnection.InstanceLockTableName} WHERE name = @name", true);
            query.BindParam("name", name);
            query.ExecuteReader();
            if (!query.ReadNext())
                return null;
            return query.GetValue<string>(0);
        }

        [Fact]
        public void Acquire_CreatesTable_AndSetsHandleState()
        {
            using var connection = Open();

            using IDbInstanceLock handle = connection.AcquireInstanceLock(LockName, ShortWait, LongLease);

            handle.Name.Should().Be(LockName);
            handle.IsHeld.Should().BeTrue();
            connection.DoesObjectExist(SqlDbConnection.InstanceLockTableName, null, "table").Should().BeTrue();
        }

        [Fact]
        public void ContendedLock_TimesOut_ThenSucceedsAfterRelease()
        {
            using var holder = Open();
            using var contender = Open();

            IDbInstanceLock first = holder.AcquireInstanceLock(LockName, ShortWait, LongLease);

            // A second connection cannot take the same lock while it is held.
            contender.TryAcquireInstanceLock(LockName, ShortWait, out IDbInstanceLock blocked, LongLease)
                .Should().BeFalse();
            blocked.Should().BeNull();

            // Once released, the second connection acquires it.
            first.Dispose();

            contender.TryAcquireInstanceLock(LockName, ShortWait, out IDbInstanceLock acquired, LongLease)
                .Should().BeTrue();
            acquired.Should().NotBeNull();
            acquired.Dispose();
        }

        [Fact]
        public void Acquire_Throws_LockTimeout_WhenHeld()
        {
            using var holder = Open();
            using var contender = Open();

            using IDbInstanceLock first = holder.AcquireInstanceLock(LockName, ShortWait, LongLease);

            Action act = () => contender.AcquireInstanceLock(LockName, ShortWait, LongLease);

            act.Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.LockTimeout);
        }

        [Fact]
        public void ExpiredLease_IsReclaimed()
        {
            using var connection = Open();

            // Bootstrap the table/row, then plant a still-owned but expired lease (epoch 1 = 1970).
            using (connection.AcquireInstanceLock(LockName, ShortWait, LongLease)) { }
            using (var query = connection.GetQuery(
                $"UPDATE {SqlDbConnection.InstanceLockTableName} SET owner = 'stale', expires_at = 1 WHERE name = @name", true))
            {
                query.BindParam("name", LockName);
                query.ExecuteNoData();
            }

            // A brand new acquirer reclaims the expired lease without waiting out the timeout.
            using IDbInstanceLock handle = connection.AcquireInstanceLock(LockName, Immediate, LongLease);
            handle.IsHeld.Should().BeTrue();
        }

        [Fact]
        public void Release_ClearsOnlyOwnToken()
        {
            using var connection = Open();

            IDbInstanceLock handle = connection.AcquireInstanceLock(LockName, ShortWait, LongLease);

            // Simulate the lease having been reclaimed by another owner (still valid, far future).
            using (var query = connection.GetQuery(
                $"UPDATE {SqlDbConnection.InstanceLockTableName} SET owner = 'other', expires_at = {int.MaxValue} WHERE name = @name", true))
            {
                query.BindParam("name", LockName);
                query.ExecuteNoData();
            }

            // Disposing our now-stale handle must not clear someone else's ownership.
            handle.Dispose();

            ReadOwner(connection, LockName).Should().Be("other");
        }

        [Fact]
        public void Dispose_IsIdempotent()
        {
            using var connection = Open();

            IDbInstanceLock handle = connection.AcquireInstanceLock(LockName, ShortWait, LongLease);
            handle.Dispose();
            handle.IsHeld.Should().BeFalse();

            Action second = () => handle.Dispose();
            second.Should().NotThrow();
            handle.IsHeld.Should().BeFalse();
        }

        [Fact]
        public void Reacquire_SameName_SameConnection_Throws()
        {
            using var connection = Open();

            using IDbInstanceLock handle = connection.AcquireInstanceLock(LockName, ShortWait, LongLease);

            Action act = () => connection.AcquireInstanceLock(LockName, ShortWait, LongLease);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Reacquire_AfterRelease_Succeeds()
        {
            using var connection = Open();

            connection.AcquireInstanceLock(LockName, ShortWait, LongLease).Dispose();

            // The name is unregistered on release, so the same connection can take it again.
            using IDbInstanceLock again = connection.AcquireInstanceLock(LockName, ShortWait, LongLease);
            again.IsHeld.Should().BeTrue();
        }

        [Fact]
        public void DifferentNames_DoNotContend()
        {
            using var connection = Open();

            using IDbInstanceLock a = connection.AcquireInstanceLock("lock-a", ShortWait, LongLease);
            using IDbInstanceLock b = connection.AcquireInstanceLock("lock-b", ShortWait, LongLease);

            a.IsHeld.Should().BeTrue();
            b.IsHeld.Should().BeTrue();
        }
    }
}

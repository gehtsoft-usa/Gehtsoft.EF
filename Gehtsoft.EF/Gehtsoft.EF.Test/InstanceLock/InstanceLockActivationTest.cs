using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.InstanceLock;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.InstanceLock
{
    /// <summary>
    /// Cross-driver smoke test: on every configured database the instance lock simply activates -
    /// acquire succeeds, the handle reports held, release completes, and the name can be taken again.
    /// This proves the lease fallback (and, once added, the native advisory locks) light up on each
    /// platform, not only SQLite. Deeper lease semantics are covered by <see cref="LeaseInstanceLockTest"/>.
    /// </summary>
    public sealed class InstanceLockActivationTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
        // Bounded lease so a crashed run self-heals quickly (the fixed names are reused every run).
        private static readonly TimeSpan Lease = TimeSpan.FromSeconds(15);

        private readonly SqlConnectionFixtureBase mFixture;

        public InstanceLockActivationTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AcquireRelease_Activates(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            const string name = "eflock_smoke_acquire";

            IDbInstanceLock handle = connection.AcquireInstanceLock(name, Wait, Lease);
            handle.Should().NotBeNull();
            handle.IsHeld.Should().BeTrue();
            handle.Name.Should().Be(name);

            handle.Dispose();
            handle.IsHeld.Should().BeFalse();

            // Re-acquiring after release proves the lock was truly freed on this platform.
            using IDbInstanceLock again = connection.AcquireInstanceLock(name, Wait, Lease);
            again.IsHeld.Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void TryAcquireRelease_Activates(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);
            const string name = "eflock_smoke_try";

            connection.TryAcquireInstanceLock(name, Wait, out IDbInstanceLock handle, Lease)
                .Should().BeTrue();
            handle.Should().NotBeNull();
            handle.IsHeld.Should().BeTrue();

            handle.Dispose();
            handle.IsHeld.Should().BeFalse();
        }
    }
}

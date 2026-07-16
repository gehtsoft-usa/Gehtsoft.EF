using System;

namespace Gehtsoft.EF.Db.SqlDb.InstanceLock
{
    /// <summary>
    /// A handle to a held database-instance-wide advisory lock.
    ///
    /// The lock serializes coarse critical sections (for example a schema update) between
    /// processes that share the same database. Acquire it with
    /// <see cref="SqlDbConnection.AcquireInstanceLock(string, TimeSpan, TimeSpan?)"/> or
    /// <see cref="SqlDbConnection.TryAcquireInstanceLock(string, TimeSpan, out IDbInstanceLock, TimeSpan?)"/>,
    /// and hold it for the whole critical section - because DDL auto-commits on some engines the
    /// lock is session/advisory-scoped, not transaction-scoped. Release it by disposing the handle.
    ///
    /// The lock is <b>not reentrant</b>: acquiring a name that the same connection already holds
    /// throws immediately rather than deadlocking.
    /// </summary>
    public interface IDbInstanceLock : IDisposable
    {
        /// <summary>
        /// The name of the lock (the resource this handle serializes access to).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the lock is currently held by this handle. Becomes `false` once the handle is
        /// disposed.
        /// </summary>
        bool IsHeld { get; }
    }
}

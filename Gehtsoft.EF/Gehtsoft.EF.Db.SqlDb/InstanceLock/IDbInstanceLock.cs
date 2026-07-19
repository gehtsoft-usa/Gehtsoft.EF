using System;

namespace Gehtsoft.EF.Db.SqlDb.InstanceLock
{
    /// <summary>
    /// <para>A handle to a held database-instance-wide advisory lock.</para>
    /// <para>The lock serializes coarse critical sections (for example a schema update) between processes
    /// that share the same database. Acquire it with the [c]AcquireInstanceLock[/c] or
    /// [c]TryAcquireInstanceLock[/c] methods of
    /// [clink=Gehtsoft.EF.Db.SqlDb.SqlDbConnection]SqlDbConnection[/clink], and hold it for the whole
    /// critical section - because DDL auto-commits on some engines the lock is session/advisory-scoped, not
    /// transaction-scoped. Release it by disposing the handle.</para>
    /// <para>The lock is not reentrant: acquiring a name that the same connection already holds throws
    /// immediately rather than deadlocking.</para>
    /// </summary>
    public interface IDbInstanceLock : IDisposable
    {
        /// <summary>
        /// The name of the lock (the resource this handle serializes access to).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the lock is currently held by this handle (becomes false once the handle is disposed).
        /// </summary>
        bool IsHeld { get; }
    }
}

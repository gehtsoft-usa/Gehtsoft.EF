using System;

namespace Gehtsoft.EF.Db.SqlDb.InstanceLock
{
    /// <summary>
    /// The handle returned by the portable lease fallback (<c>ef_catalog_lock</c>). Disposing it
    /// clears the lease row's owner token (only if it is still this handle's token) and unregisters
    /// the name from the owning connection. Dispose is idempotent.
    /// </summary>
    internal sealed class LeaseInstanceLock : IDbInstanceLock
    {
        private SqlDbConnection mConnection;
        private readonly string mOwner;

        public string Name { get; }

        public bool IsHeld { get; private set; }

        internal LeaseInstanceLock(SqlDbConnection connection, string name, string owner)
        {
            mConnection = connection;
            Name = name;
            mOwner = owner;
            IsHeld = true;
        }

        public void Dispose()
        {
            if (!IsHeld)
                return;
            IsHeld = false;

            SqlDbConnection connection = mConnection;
            mConnection = null;
            if (connection == null)
                return;

            try
            {
                connection.ReleaseInstanceLockLease(Name, mOwner);
            }
            catch
            {
                // Best-effort release: if the connection is already gone the lease simply expires on
                // its own, so a failure here must not surface from Dispose.
            }
            finally
            {
                connection.UnregisterInstanceLock(Name);
            }
        }
    }
}

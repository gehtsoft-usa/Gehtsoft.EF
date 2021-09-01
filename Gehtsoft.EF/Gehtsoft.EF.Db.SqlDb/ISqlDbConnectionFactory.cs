using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The interface for SQL connection factory to be used in dependency injection.
    /// </summary>
    public interface ISqlDbConnectionFactory
    {
        /// <summary>
        /// The flag indicating whether the connections returned by this factory
        /// needs to be disposed.
        /// </summary>
        bool NeedDispose { get; }

        /// <summary>
        /// Creates a new connection.
        ///
        /// Dispose the connection if <see cref="NeedDispose"/> flag is true.
        /// </summary>
        /// <returns></returns>
        SqlDbConnection GetConnection();
        /// <summary>
        /// Creates a new connection asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null);
    }

    /// <summary>
    /// The implementation of the factory interface for dependency injection systems.
    ///
    /// The implementation uses <see cref="UniversalSqlDbFactory"/>
    /// </summary>
    public class SqlDbUniversalConnectionFactory : ISqlDbConnectionFactory
    {
        private readonly string mDriver, mConnectionString;

        /// <summary>
        /// The flag indicating whether the connections needs to be disposed.
        /// </summary>
        [DocgenIgnore]
        public bool NeedDispose => true;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="driver"></param>
        /// <param name="connectionString"></param>
        public SqlDbUniversalConnectionFactory(string driver, string connectionString)
        {
            mDriver = driver;
            mConnectionString = connectionString;
        }

        /// <summary>
        /// Gets connection.
        /// </summary>
        /// <returns></returns>
        [DocgenIgnore]
        public SqlDbConnection GetConnection() => UniversalSqlDbFactory.Create(mDriver, mConnectionString);

        /// <summary>
        /// Gets connection asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        [DocgenIgnore]
        public Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null) => UniversalSqlDbFactory.CreateAsync(mDriver, mConnectionString, token ?? CancellationToken.None);
    }

    /// <summary>
    /// The factory that uses the existing connection
    /// </summary>
    public class ExistingConnectionFactory : ISqlDbConnectionFactory
    {
        private readonly SqlDbConnection mConnection;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection"></param>
        public ExistingConnectionFactory(SqlDbConnection connection)
        {
            mConnection = connection;
        }

        [DocgenIgnore]
        public bool NeedDispose => false;

        [DocgenIgnore]
        public SqlDbConnection GetConnection()
        {
            return mConnection;
        }

        [DocgenIgnore]
        public Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null)
        {
            return Task.FromResult(mConnection);
        }
    }
}
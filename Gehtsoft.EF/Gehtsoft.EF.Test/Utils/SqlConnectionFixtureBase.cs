using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Test.Utils
{
    public class SqlConnectionFixtureBase : IDisposable
    {
        private readonly Dictionary<string, SqlDbConnection> mConnection = new Dictionary<string, SqlDbConnection>();

        public bool Started(string connectionName) => mConnection.ContainsKey(connectionName);

        public SqlConnectionFixtureBase()
        {
        }

        ~SqlConnectionFixtureBase()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            foreach (var connection in mConnection.Values)
            {
                TearDownConnection(connection);
                connection?.Dispose();
            }
        }

        public SqlDbConnection GetInstance(string connection) => GetInstance(connection, AppConfiguration.Instance.Get("sqlConnections:" + connection));

        public SqlDbConnection GetInstance(string connectionName, string connectionString)
        {
            var key = connectionName;
            var config = AppConfiguration.Instance.GetSqlConnection(connectionName);

            if (mConnection.TryGetValue(key, out var connection))
                return connection;

            connection = UniversalSqlDbFactory.Create(config.Driver, config.ConnectionString);

            if (connection == null)
                throw new ArgumentException($"Incorrect driver name or connection settings {connectionName}:{connectionString}");

            ConfigureConnection(connection);

            mConnection[key] = connection;

            return connection;
        }

        protected virtual void ConfigureConnection(SqlDbConnection connection) { }

        protected virtual void TearDownConnection(SqlDbConnection connection) { }
    }
}

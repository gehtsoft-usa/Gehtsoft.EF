using System;
using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.MongoDb;

namespace Gehtsoft.EF.Test.Utils
{
    public class MongoConnectionFixtureBase : IDisposable
    {
        private readonly Dictionary<string, MongoConnection> mConnection = new Dictionary<string, MongoConnection>();

        public bool Started(string connectionName) => mConnection.ContainsKey(connectionName);

        public MongoConnectionFixtureBase()
        {
        }

        ~MongoConnectionFixtureBase()
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

        public MongoConnection GetInstance(string connection) => GetInstance(connection, AppConfiguration.Instance.Get("nosqlConnections:" + connection));

        public MongoConnection GetInstance(string connectionName, string connectionString)
        {
            var key = connectionName;
            var config = AppConfiguration.Instance.GetNoSqlConnection(connectionName);

            if (mConnection.TryGetValue(key, out var connection))
                return connection;

            if (config?.Driver != "mongo")
                throw new ArgumentException($"Connection {connectionName} is expected to be a mongo connection but it is {config?.Driver}", nameof(connectionName));

            connection = MongoConnectionFactory.Create(config.ConnectionString);

            ConfigureConnection(connection);

            mConnection[key] = connection;

            return connection;
        }

        protected virtual void ConfigureConnection(MongoConnection connection) { }

        protected virtual void TearDownConnection(MongoConnection connection) { }
    }
}

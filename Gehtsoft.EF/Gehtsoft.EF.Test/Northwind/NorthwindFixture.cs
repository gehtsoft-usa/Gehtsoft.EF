using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Northwind
{
    public sealed class NorthwindFixture : IDisposable
    {
        private readonly Snapshot mSnapshot = new Snapshot();
        private readonly Dictionary<string, SqlDbConnection> gConnections = new Dictionary<string, SqlDbConnection>();

        public Snapshot Snapshot => mSnapshot;

        public NorthwindFixture()
        {
        }

        public SqlDbConnection GetInstance(string connection) => GetInstance(connection, AppConfiguration.Instance.Get("connections:" + connection));

        public SqlDbConnection GetInstance(string connectionName, string connectionString)
        {
            var key = connectionName;
            var config = AppConfiguration.Instance.GetSqlConnection(connectionName);

            if (gConnections.TryGetValue(key, out var connection))
                return connection;

            connection = UniversalSqlDbFactory.Create(config.Driver, config.ConnectionString);

            if (connection == null)
                throw new ArgumentException($"Incorrect driver name or connection settings {connectionName}:{connectionString}");

            mSnapshot.Create(connection, 100);
            gConnections[key] = connection;
            return connection;
        }

        public void Dispose()
        {
            foreach (var connection in gConnections.Values)
                connection?.Dispose();
        }
    }

    [CollectionDefinition(nameof(NorthwindFixture))]
    public class NorthwindFixtureCollection : ICollectionFixture<NorthwindFixture>
    {
    }
}

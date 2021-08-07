using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Northwind;
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

        public SqlDbConnection GetInstance(string driver) => GetInstance(driver, TestConfiguration.Instance.Get("connections:" + driver));

        public SqlDbConnection GetInstance(string driver, string connectionString)
        {
            var key = driver + "," + connectionString;
            if (gConnections.TryGetValue(key, out var connection))
                return connection;

            connection = UniversalSqlDbFactory.Create(driver, connectionString);

            if (connection == null)
                throw new ArgumentException($"Incorrect driver name or connection settings {driver}:{connectionString}");

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

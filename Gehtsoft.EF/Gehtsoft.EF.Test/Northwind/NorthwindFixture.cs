using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.OracleDb;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.MongoDb;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Northwind
{
    public sealed class NorthwindFixture : ConnectionFixtureBase
    {
        public Snapshot Snapshot { get; } = new Snapshot();

        public NorthwindFixture()
        {
        }

        protected override void ConfigureConnection(SqlDbConnection connection)
        {
            Snapshot.Create(connection, 100);
            if (connection.ConnectionType == UniversalSqlDbFactory.ORACLE &&
                connection is OracleDbConnection oracleConnection)
            {
                oracleConnection.UpdateSequence(typeof(Category));
                oracleConnection.UpdateSequence(typeof(Employee));
                oracleConnection.UpdateSequence(typeof(Product));
                oracleConnection.UpdateSequence(typeof(Region));
                oracleConnection.UpdateSequence(typeof(Order));
                oracleConnection.UpdateSequence(typeof(Shipper));
                oracleConnection.UpdateSequence(typeof(Supplier));
            }
            base.ConfigureConnection(connection);
        }
    }

    [CollectionDefinition(nameof(NorthwindFixture))]
    public class NorthwindFixtureCollection : ICollectionFixture<NorthwindFixture>
    {
    }
}

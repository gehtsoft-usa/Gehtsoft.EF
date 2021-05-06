using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Validator
{
    public class ValidatorConnectionFactory : IValidatorConnectionFactory
    {
        private readonly SqlDbConnectionFactory mFactory;
        private readonly SqlDbConnectionFactoryAsync mAsyncFactory;
        private readonly string mConnectionString;

        public ValidatorConnectionFactory(SqlDbConnectionFactory factory, string connectionString) : this(factory, null, connectionString)
        {
        }

        public ValidatorConnectionFactory(SqlDbConnectionFactory factory, SqlDbConnectionFactoryAsync asyncFactory, string connectionString)
        {
            mFactory = factory;
            mAsyncFactory = asyncFactory;
            mConnectionString = connectionString;
        }

        public bool NeedToDispose => true;
        public SqlDbConnection GetConnection() => mFactory(mConnectionString);
        public Task<SqlDbConnection> GetConnectionAsync() => mAsyncFactory(mConnectionString, null);

        public Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null) => mAsyncFactory(mConnectionString, token);
    }
}
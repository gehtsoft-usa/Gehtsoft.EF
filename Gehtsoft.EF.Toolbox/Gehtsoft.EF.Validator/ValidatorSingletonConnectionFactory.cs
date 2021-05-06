using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Validator
{
    public class ValidatorSingletonConnectionFactory : IValidatorConnectionFactory
    {
        private readonly SqlDbConnection mConnection;

        public ValidatorSingletonConnectionFactory(SqlDbConnection connection)
        {
            mConnection = connection;
        }
        public bool NeedToDispose => false;
        public SqlDbConnection GetConnection() => mConnection;
        public Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null) => Task.FromResult(mConnection);
    }
}
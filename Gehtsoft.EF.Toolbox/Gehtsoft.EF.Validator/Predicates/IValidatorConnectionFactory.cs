using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Validator
{
    public interface IValidatorConnectionFactory
    {
        bool NeedToDispose { get; }
        SqlDbConnection GetConnection();
        Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null);
    }
}
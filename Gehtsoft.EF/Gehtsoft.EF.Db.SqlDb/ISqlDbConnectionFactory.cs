using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb
{
    public interface ISqlDbConnectionFactory
    {
        bool NeedDispose { get; }

        SqlDbConnection GetConnection();

        Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null);
    }

    public class SqlDbUniversalConnectionFactory : ISqlDbConnectionFactory
    {
        private readonly string mDriver, mConnectionString;

        public bool NeedDispose => true;

        public SqlDbUniversalConnectionFactory(string driver, string connectionString)
        {
            mDriver = driver;
            mConnectionString = connectionString;
        }

        public SqlDbConnection GetConnection() => UniversalSqlDbFactory.Create(mDriver, mConnectionString);

        public Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token) => UniversalSqlDbFactory.CreateAsync(mDriver, mConnectionString, token ?? CancellationToken.None);
    }
}
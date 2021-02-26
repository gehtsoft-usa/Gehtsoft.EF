using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Data.Sqlite;

using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Threading.Tasks;
using System.Threading;

namespace Gehtsoft.EF.Db.SqliteDb
{
    public class SqliteDbConnection : SqlDbConnection
    {
        protected SqliteConnection mSqlConnection;
        private SqliteDbTransaction mCurrentTransaction;

        public override string ConnectionType => "sqlite";

        public SqliteDbConnection(SqliteConnection connection) : base(connection)
        {
            mSqlConnection = connection;
        }

        protected override SqlDbQuery ConstructQuery()
        {
            return new SqliteDbQuery(this, mSqlConnection.CreateCommand(), gSpecifics);
        }

        protected override SqlDbQuery ConstructQuery(string queryText)
        {
            return new SqliteDbQuery(this, mSqlConnection.CreateCommand(), gSpecifics) { CommandText = queryText };
        }

        public override SqlDbLanguageSpecifics GetLanguageSpecifics()
        {
            return gSpecifics;
        }

        private static readonly SqliteDbLanguageSpecifics gSpecifics = new SqliteDbLanguageSpecifics();

        public override CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new SqliteCreateTableBuilder(gSpecifics, descriptor);
        }

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement)
        {
            return new SqliteInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement)
        {
            return new SqliteInsertSelectQueryBuilder(gSpecifics, descriptor, selectQuery, ignoreAutoIncrement);
        }

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null)
        {
            return new SqliteHierarchicalSelectQueryBuilder(gSpecifics, descriptor, parentReferenceColumn, rootParameter);
        }

        protected override async Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token)
        {
            List<TableDescriptor> tables = new List<TableDescriptor>();

            using (SqlDbQuery query = GetQuery("select NAME from SQLITE_MASTER where TYPE=@type"))
            {
                query.BindParam("type", "table");
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)));
                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)));
                }
            }

            using (SqlDbQuery query = GetQuery("select NAME from SQLITE_MASTER where TYPE=@type"))
            {
                query.BindParam("type", "view");
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = true } );
                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = true });
                }
            }

            foreach (TableDescriptor descriptor in tables)
            {
                using (SqlDbQuery query = GetQuery($"pragma table_info({descriptor.Name})"))
                {
                    if (sync)
                    {
                        query.ExecuteReader();
                        while (query.ReadNext())
                        {
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.GetValue<string>("name") });
                        }
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        while (await query.ReadNextAsync(token))
                        {
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.GetValue<string>("name") });
                        }
                    }
                }
            }

            return tables.ToArray();
        }

        public override AlterTableQueryBuilder GetAlterTableQueryBuilder()
        {
            return new SqliteAlterTableQueryBuilder(GetLanguageSpecifics());
        }

        public override SqlDbTransaction BeginTransaction()
        {
            if (mCurrentTransaction != null)
                return new SqliteDbTransaction(this);
            else
            {
                mCurrentTransaction = new SqliteDbTransaction(this, mSqlConnection.BeginTransaction());
                return mCurrentTransaction;
            }
        }

        internal void EndTransaction(SqliteDbTransaction transaction)
        {
            if (!transaction.IsSavePoint)
                mCurrentTransaction = null;
        }
    }

    public static class SqliteDbConnectionFactory
    {
        public static bool IncludeVersion { get; set; } = false;

        public static SqlDbConnection Create(string connectionString)
        {
            Microsoft.Data.Sqlite.SqliteConnection connection = new Microsoft.Data.Sqlite.SqliteConnection();
            if (IncludeVersion && !connectionString.Contains("Version="))
                connectionString += ";Version=3;";
            connection.ConnectionString = connectionString;
            connection.Open();
            return new SqliteDbConnection(connection);
        }

        public static SqlDbConnection CreateMemory()
        {
            return Create("Data Source=:memory:");
        }

        public static SqlDbConnection CreateFile(string file, bool createNew, string password = null)
        {
            if (createNew && File.Exists(file))
                File.Delete(file);

            StringBuilder connectionString = new StringBuilder($"Data Source={file};");

#if !NETCORE
            if (createNew)
                connectionString.Append("New=True;");
#endif

            if (password != null)
                connectionString.Append($"Password={password};");

            return Create(connectionString.ToString());
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            Microsoft.Data.Sqlite.SqliteConnection connection = new Microsoft.Data.Sqlite.SqliteConnection();
            if (IncludeVersion && !connectionString.Contains("Version="))
                connectionString += ";Version=3;";
            connection.ConnectionString = connectionString;
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new SqliteDbConnection(connection);
        }
    }
}
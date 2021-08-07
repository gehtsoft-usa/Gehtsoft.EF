using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Threading.Tasks;
using System.Threading;
using MySqlConnector;
using System.Data;

namespace Gehtsoft.EF.Db.MysqlDb
{
    public class MysqlDbConnection : SqlDbConnection
    {
        protected MySqlConnection mSqlConnection;
        protected MySqlTransaction mCurrentTransaction;

        public override string ConnectionType => "mysql";

        public MysqlDbConnection(MySqlConnection connection) : base(connection)
        {
            mSqlConnection = connection;
        }

        protected override SqlDbQuery ConstructQuery()
        {
            return new MysqlDbQuery(this, new MySqlCommand("", mSqlConnection, mCurrentTransaction), gSpecifics);
        }

        protected override SqlDbQuery ConstructQuery(string queryText)
        {
            return new MysqlDbQuery(this, new MySqlCommand("", mSqlConnection, mCurrentTransaction), gSpecifics) { CommandText = queryText };
        }

        public override SqlDbTransaction BeginTransaction()
        {
            if (mCurrentTransaction != null)
                throw new EfSqlException(EfExceptionCode.NestingTransactionsNotSupported);

            mCurrentTransaction = mSqlConnection.BeginTransaction();
            return new MysqlDbTransaction(this, mCurrentTransaction);
        }

        public override SqlDbTransaction BeginTransaction(IsolationLevel level)
        {
            if (mCurrentTransaction != null)
                throw new EfSqlException(EfExceptionCode.NestingTransactionsNotSupported);

            mCurrentTransaction = mSqlConnection.BeginTransaction(level);
            return new MysqlDbTransaction(this, mCurrentTransaction);
        }

        internal void EndTransaction()
        {
            mCurrentTransaction = null;
        }

        private static readonly MysqlDbLanguageSpecifics gSpecifics = new MysqlDbLanguageSpecifics();

        public override SqlDbLanguageSpecifics GetLanguageSpecifics()
        {
            return gSpecifics;
        }

        public override CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new MysqlCreateTableBuilder(gSpecifics, descriptor);
        }

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement)
        {
            return new MysqlInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement)
        {
            return new MysqlInsertSelectQueryBuilder(gSpecifics, descriptor, selectQuery, ignoreAutoIncrement);
        }

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null)
        {
            return new MysqlHierarchicalSelectQueryBuilder(gSpecifics, descriptor, parentReferenceColumn, rootParameter);
        }

        protected override async Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token)
        {
            List<TableDescriptor> tables = new List<TableDescriptor>();

            using (SqlDbQuery query = GetQuery("show full tables"))
            {
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = query.GetValue<string>(1) == "VIEW" });
                }
                else
                {
                    await query.ExecuteReaderAsync(token);
                    while (await query.ReadNextAsync(token))
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = query.GetValue<string>(1) == "VIEW" });
                }
            }

            foreach (TableDescriptor descriptor in tables)
            {
                using (SqlDbQuery query = GetQuery($"select * from {descriptor.Name} where false"))
                {
                    if (sync)
                    {
                        query.ExecuteReader();
                        query.ReadNext();
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        await query.ReadNextAsync(token);
                    }

                    for (int i = 0; i < query.FieldCount; i++)
                        descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.Reader.GetName(i) });
                }
            }

            return tables.ToArray();
        }

        public override AlterTableQueryBuilder GetAlterTableQueryBuilder()
        {
            return new MysqlAlterTableQueryBuilder(GetLanguageSpecifics());
        }
    }

    public static class MysqlDbConnectionFactory
    {
        public static SqlDbConnection Create(string connectionString)
        {
            MySqlConnection connection = new MySqlConnector.MySqlConnection
            {
                ConnectionString = connectionString
            };
            connection.Open();
            return new MysqlDbConnection(connection);
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            MySqlConnection connection = new MySqlConnector.MySqlConnection
            {
                ConnectionString = connectionString
            };
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new MysqlDbConnection(connection);
        }
    }
}
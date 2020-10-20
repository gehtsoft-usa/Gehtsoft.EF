using System.Collections.Generic;
using Npgsql;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Threading.Tasks;
using System.Threading;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDbConnection : SqlDbConnection
    {
        protected NpgsqlConnection mSqlConnection;
        protected NpgsqlTransaction mCurrentTransaction;

        public override string ConnectionType => "npgsql";

        public PostgresDbConnection(NpgsqlConnection connection) : base(connection)
        {
            mSqlConnection = connection;
        }

        protected override SqlDbQuery ConstructQuery()
        {
            return new PostgresDbQuery(this, new NpgsqlCommand("", mSqlConnection, mCurrentTransaction), gSpecifics);
        }

        protected override SqlDbQuery ConstructQuery(string queryText)
        {
            return new PostgresDbQuery(this, new NpgsqlCommand("", mSqlConnection, mCurrentTransaction), gSpecifics) { CommandText = queryText };
        }

        public override SqlDbTransaction BeginTransaction()
        {
            if (mCurrentTransaction != null)
            {
                //create savepoint transaction
                return new PostgresDbTransaction(this);
            }
            else
            {
                mCurrentTransaction = mSqlConnection.BeginTransaction();
                return new PostgresDbTransaction(this, mCurrentTransaction);
            }
        }

        internal void EndTransaction(PostgresDbTransaction transaction)
        {
            if (!transaction.IsSavePoint)
                mCurrentTransaction = null;
        }

        private static readonly PostgresDbLanguageSpecifics gSpecifics = new PostgresDbLanguageSpecifics();

        public override SqlDbLanguageSpecifics GetLanguageSpecifics()
        {
            return gSpecifics;
        }

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement)
        {
            return new PostgresInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement)
        {
            return new PostgresInsertSelectQueryBuilder(gSpecifics, descriptor, selectQuery, ignoreAutoIncrement);
        }

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null)
        {
            return new PostgresHierarchicalSelectQueryBuilder(gSpecifics, descriptor, parentReferenceColumn, rootParameter);
        }

        protected override async Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token)
        {
            List<TableDescriptor> tables = new List<TableDescriptor>();

            using (SqlDbQuery query = GetQuery("select tablename from pg_catalog.pg_tables where schemaname=current_schema()"))
            {
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

            foreach (TableDescriptor descriptor in tables)
            {
                using (SqlDbQuery query = GetQuery($"select * from {descriptor.Name} where false"))
                {
                    if (sync)
                    {
                        query.ExecuteReader();
                        query.ReadNext();
                        for (int i = 0; i < query.FieldCount; i++)
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.Reader.GetName(i) });
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        await query.ReadNextAsync(token);
                        for (int i = 0; i < query.FieldCount; i++)
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.Reader.GetName(i) });
                    }
                }
            }

            return tables.ToArray();
        }

        public override AlterTableQueryBuilder GetAlterTableQueryBuilder()
        {
            return new PostgresAlterTableQueryBuilder(GetLanguageSpecifics());
        }
    }

    public static class PostgresDbConnectionFactory
    {
        public static SqlDbConnection Create(string connectionString)
        {
            NpgsqlConnection connection = new NpgsqlConnection();
            connection.ConnectionString = connectionString;
            connection.Open();
            return new PostgresDbConnection(connection);
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            NpgsqlConnection connection = new NpgsqlConnection();
            connection.ConnectionString = connectionString;
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new PostgresDbConnection(connection);
        }
    }
}
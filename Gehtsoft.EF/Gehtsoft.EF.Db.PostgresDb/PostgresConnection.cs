using System.Collections.Generic;
using Npgsql;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Threading.Tasks;
using System.Threading;
using System.Data;
using System;

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

        public override SqlDbTransaction BeginTransaction(IsolationLevel level)
        {
            if (mCurrentTransaction != null)
            {
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported, "The isolation level cannot be set of nested transactions");
            }
            else
            {
                mCurrentTransaction = mSqlConnection.BeginTransaction(level);
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

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement = false)
        {
            return new PostgresInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false)
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

            using (SqlDbQuery query = GetQuery("select viewname from pg_catalog.pg_views where schemaname=current_schema()"))
            {
                if (sync)
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        tables.Add(new TableDescriptor(query.GetValue<string>(0)) { View = true });
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

        public override DropIndexBuilder GetDropIndexBuilder(TableDescriptor descriptor, string name)
        {
            return new PostgresDropIndexBuilder(GetLanguageSpecifics(), descriptor.Name, name);
        }

        public override CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new PostgresCreateTableBuilder(GetLanguageSpecifics(), descriptor);
        }

        protected async override ValueTask<bool> DoesObjectExistCore(string tableName, string objectName, string objectType, bool executeAsync)
        {
            string query;
            if (objectType == "index")
            {
                query = $"SELECT * FROM pg_indexes WHERE tablename='{tableName}' AND indexname='{tableName}_{objectName}';";
            }
            else if (objectType == "table")
            {
                query = $"SELECT * FROM pg_tables WHERE schemaname = current_schema()  AND tablename = '{tableName}';";
            }
            else if (objectType == "view")
            {
                query = $"SELECT * FROM pg_views WHERE schemaname = current_schema()  AND viewname = '{tableName}';";
            }
            else if (objectType == "column")
            {
                query = $"SELECT * FROM information_schema.columns WHERE table_schema = current_schema() and table_name='{tableName}' and column_name='{objectName}';";
            }
            else
                throw new ArgumentException($"Unexpected type {objectType}", nameof(objectType));

            using (var stmt = GetQuery(query, true))
            {
                if (executeAsync)
                    await stmt.ExecuteReaderAsync();
                else
                    stmt.ExecuteReader();

                if (executeAsync)
                    return await stmt.ReadNextAsync();
                else
                    return stmt.ReadNext();
            }
        }
    }

    public static class PostgresDbConnectionFactory
    {
        public static bool LegacyTimestampBehavior { get; set; } = true;
        private static bool mLegacyTimestampBehaviorSet = false;

        private static void UpdateLegacyTimestampBehavior()
        {
            if (!mLegacyTimestampBehaviorSet && LegacyTimestampBehavior)
                AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public static SqlDbConnection Create(string connectionString)
        {
            UpdateLegacyTimestampBehavior();

            NpgsqlConnection connection = new NpgsqlConnection
            {
                ConnectionString = connectionString
            };
            connection.Open();
            return new PostgresDbConnection(connection);
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            UpdateLegacyTimestampBehavior();

            NpgsqlConnection connection = new NpgsqlConnection
            {
                ConnectionString = connectionString
            };
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new PostgresDbConnection(connection);
        }
    }
}


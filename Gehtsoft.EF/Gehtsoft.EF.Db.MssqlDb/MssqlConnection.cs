using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

#pragma warning disable S6966 // Awaitable method should be used

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlDbConnection : SqlDbConnection
    {
        protected SqlConnection mSqlConnection;
        protected MssqlTransaction mTransaction;

        public override string ConnectionType => "mssql";

        public MssqlDbConnection(SqlConnection connection) : base(connection)
        {
            mSqlConnection = connection;
        }

        protected override SqlDbQuery ConstructQuery()
        {
            return GetQuery1(null);
        }

        protected override SqlDbQuery ConstructQuery(string queryText)
        {
            return GetQuery1(queryText);
        }

        protected virtual MssqlQuery GetQuery1(string queryText)
        {
            MssqlQuery query = new MssqlQuery(this, mSqlConnection.CreateCommand(), GetLanguageSpecifics());
            if (mTransaction != null)
                query.SetTransaction(mTransaction);
            if (queryText != null)
                query.CommandText = queryText;
            return query;
        }

        private int mSavePointID = 1;

        public override SqlDbTransaction BeginTransaction()
        {
            if (mTransaction == null)
            {
                MssqlTransaction t = new MssqlTransaction(this, mSqlConnection.BeginTransaction());
                mTransaction = t;
                return t;
            }
            else
            {
                MssqlTransaction t = new MssqlTransaction(this, mTransaction.DbTransaction, "sp" + mSavePointID);
                mSavePointID++;
                return t;
            }
        }

        public override SqlDbTransaction BeginTransaction(IsolationLevel level)
        {
            if (mTransaction == null)
            {
                MssqlTransaction t = new MssqlTransaction(this, mSqlConnection.BeginTransaction(level));
                mTransaction = t;
                return t;
            }
            else
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported, "The isolation level cannot be set of nested transactions");
        }

        internal virtual void EndTransaction(MssqlTransaction transaction)
        {
            if (mTransaction == transaction)
                mTransaction = null;
        }

        private static readonly MssqlDbLanguageSpecifics gSpecifics = new MssqlDbLanguageSpecifics();

        public override SqlDbLanguageSpecifics GetLanguageSpecifics()
        {
            return gSpecifics;
        }

        public override DropTableBuilder GetDropTableBuilder(TableDescriptor descriptor)
        {
            return new MssqlDropQueryBuilder(gSpecifics, descriptor);
        }

        public override DropViewBuilder GetDropViewBuilder(string name)
        {
            return new MssqlDropViewBuilder(gSpecifics, name);
        }

        public override DropIndexBuilder GetDropIndexBuilder(TableDescriptor descriptor, string name)
        {
            return new MssqlDropIndexBuilder(gSpecifics, descriptor.Name, name);
        }

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement = false)
        {
            return new MssqlInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null)
        {
            return new MssqlHierarchicalSelectQueryBuilder(gSpecifics, descriptor, parentReferenceColumn, rootParameter);
        }

        public override SelectQueryBuilder GetSelectQueryBuilder(TableDescriptor descriptor)
        {
            return new MssqlSelectQueryBuilder(gSpecifics, descriptor);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false)
        {
            return new MssqlInsertSelectQueryBuilder(gSpecifics, descriptor, selectQuery, ignoreAutoIncrement);
        }

        protected override async Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token)
        {
            List<TableDescriptor> tables = new List<TableDescriptor>();

            using (SqlDbQuery query = GetQuery("select TABLE_NAME, TABLE_TYPE from INFORMATION_SCHEMA.TABLES where TABLE_SCHEMA = (select SCHEMA_NAME())"))
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
                using (SqlDbQuery query = GetQuery("select COLUMN_NAME from INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = (SELECT SCHEMA_NAME()) and TABLE_NAME = @p1"))
                {
                    query.BindParam("p1", descriptor.Name);
                    if (sync)
                    {
                        query.ExecuteReader();
                        while (query.ReadNext())
                        {
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.GetValue<string>(0) });
                        }
                    }
                    else
                    {
                        await query.ExecuteReaderAsync(token);
                        while (await query.ReadNextAsync(token))
                        {
                            descriptor.Add(new TableDescriptor.ColumnInfo() { Name = query.GetValue<string>(0) });
                        }
                    }
                }
            }

            return tables.ToArray();
        }

        public override AlterTableQueryBuilder GetAlterTableQueryBuilder()
        {
            return new MssqlAlterTableQueryBuilder(GetLanguageSpecifics());
        }

        public override CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new MssqlCreateTableBuilder(GetLanguageSpecifics(), descriptor);
        }

        protected async override ValueTask<bool> DoesObjectExistCore(string tableName, string objectName, string objectType, bool executeAsync)
        {
            string query;
            if (objectType == "index")
            {
                query = $"IndexProperty(Object_Id('{tableName}'), '{tableName}_{objectName}', 'IndexID')";
            }
            else if (objectType == "table")
            {
                query = $"OBJECT_ID ('{tableName}', 'U')";
            }
            else if (objectType == "view")
            {
                query = $"OBJECT_ID ('{tableName}', 'V')";
            }
            else if (objectType == "column")
            {
                query = $"COL_LENGTH('{tableName}','{objectName}')";
            }
            else
                throw new ArgumentException($"Unexpected type {objectType}", nameof(objectType));

            using (var stmt = GetQuery($"SELECT {query};", true))
            {
                if (executeAsync)
                    await stmt.ExecuteReaderAsync();
                else
                    stmt.ExecuteReader();

                if (executeAsync)
                    await stmt.ReadNextAsync();
                else
                    stmt.ReadNext();
                return !stmt.IsNull(0);
            }
        }
    }

    public static class MssqlDbConnectionFactory
    {
        public static SqlDbConnection Create(string connectionString)
        {
            var connection = new Microsoft.Data.SqlClient.SqlConnection
            {
                ConnectionString = connectionString
            };
            connection.Open();
            return new MssqlDbConnection(connection);
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection
            {
                ConnectionString = connectionString
            };
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new MssqlDbConnection(connection);
        }
    }
}
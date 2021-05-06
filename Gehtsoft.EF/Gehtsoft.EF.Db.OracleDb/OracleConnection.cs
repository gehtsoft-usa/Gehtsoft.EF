﻿using System;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Threading.Tasks;
using System.Threading;

namespace Gehtsoft.EF.Db.OracleDb
{
    public class OracleDbConnection : SqlDbConnection
    {
        protected OracleConnection mSqlConnection;
        protected OracleTransaction mCurrentTransaction;

        public override string ConnectionType => "oracle";

        public OracleDbConnection(OracleConnection connection) : base(connection)
        {
            mSqlConnection = connection;
        }

        protected override SqlDbQuery ConstructQuery()
        {
            OracleCommand command = mSqlConnection.CreateCommand();
            command.BindByName = true;
            return new OracleDbQuery(this, command, gSpecifics);
        }

        protected override SqlDbQuery ConstructQuery(string queryText)
        {
            OracleCommand command = mSqlConnection.CreateCommand();
            command.BindByName = true;
            return new OracleDbQuery(this, command, gSpecifics) { CommandText = queryText };
        }

        private static readonly OracleDbLanguageSpecifics gSpecifics = new OracleDbLanguageSpecifics();

        public override SqlDbLanguageSpecifics GetLanguageSpecifics()
        {
            return gSpecifics;
        }

        public override CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new OracleCreateTableBuilder(gSpecifics, descriptor);
        }

        public override DropTableBuilder GetDropTableBuilder(TableDescriptor descriptor)
        {
            return new OracleDropTableBuilder(gSpecifics, descriptor);
        }

        public override DropViewBuilder GetDropViewBuilder(string name)
        {
            return new OracleDropViewBuilder(gSpecifics, name);
        }

        public override InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement)
        {
            return new OracleInsertQueryBuilder(gSpecifics, descriptor, ignoreAutoIncrement);
        }

        public override InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement)
        {
            return new OracleInsertSelectQueryBuilder(gSpecifics, descriptor, selectQuery, ignoreAutoIncrement);
        }

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null)
        {
            return new OracleHierarchicalSelectQueryBuilder(gSpecifics, descriptor, parentReferenceColumn, rootParameter);
        }

        public override SelectQueryBuilder GetSelectQueryBuilder(TableDescriptor descriptor)
        {
            return new OracleSelectQueryBuilder(gSpecifics, descriptor);
        }

        protected override async Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token)
        {
            List<TableDescriptor> tables = new List<TableDescriptor>();

            using (SqlDbQuery query = GetQuery("SELECT TABLE_NAME FROM all_tables WHERE OWNER IN (select sys_context(:p1, :p2) from dual) order by table_name"))
            {
                query.BindParam("p1", "userenv");
                query.BindParam("p2", "current_schema");
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
            using (SqlDbQuery query = GetQuery("SELECT view_name FROM all_views WHERE OWNER IN (select sys_context(:p1, :p2) from dual) order by view_name"))
            {
                query.BindParam("p1", "userenv");
                query.BindParam("p2", "current_schema");
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
                using (SqlDbQuery query = GetQuery($"select * from {descriptor.Name} where 1 < 0"))
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
            return new OracleAlterTableQueryBuilder(GetLanguageSpecifics());
        }

        public void UpdateSequence(TableDescriptor descriptor, TableDescriptor.ColumnInfo column)
        {
            int currentTableMaximum = 0;
            int currentSequenceMaximum = 0;
            string sequenceName = $"{descriptor.Name}_{column.Name}";

            using (SqlDbQuery query = GetQuery($"select MAX({column.Name}) from {descriptor.Name}"))
            {
                query.ExecuteReader();
                if (query.ReadNext())
                    currentTableMaximum = query.GetValue<int>(0);
            }

            using (SqlDbQuery query = GetQuery($"select {sequenceName}.currval from dual"))
            {
                query.ExecuteReader();
                if (query.ReadNext())
                    currentSequenceMaximum = query.GetValue<int>(0);
            }

            if (currentTableMaximum >= currentSequenceMaximum)
            {
                int step = currentTableMaximum - currentSequenceMaximum + 11;
                using (SqlDbQuery query = GetQuery($"alter sequence {sequenceName} increment by {step}"))
                    query.ExecuteNoData();
                using (SqlDbQuery query = GetQuery($"select {sequenceName}.nextval from dual"))
                    query.ExecuteReader();
                using (SqlDbQuery query = GetQuery($"alter sequence {sequenceName} increment by 1"))
                    query.ExecuteNoData();
            }
        }

        public void UpdateSequence(TableDescriptor descriptor) => UpdateSequence(descriptor, descriptor.PrimaryKey);

        public void UpdateSequence(Type type)
        {
            TableDescriptor descriptor = AllEntities.Inst[type].TableDescriptor;
            if (descriptor == null)
                throw new ArgumentException("The type is not an entity", nameof(type));

            if (descriptor.PrimaryKey == null || !descriptor.PrimaryKey.Autoincrement)
                throw new ArgumentException("The entity does not have auto id", nameof(type));

            UpdateSequence(descriptor);
        }
    }

    public static class OracleDbConnectionFactory
    {
        public static SqlDbConnection Create(string connectionString)
        {
            OracleConnection connection = new OracleConnection
            {
                ConnectionString = connectionString
            };
            connection.Open();
            return new OracleDbConnection(connection);
        }

        public static async Task<SqlDbConnection> CreateAsync(string connectionString, CancellationToken? token)
        {
            OracleConnection connection = new OracleConnection
            {
                ConnectionString = connectionString
            };
            if (token == null)
                await connection.OpenAsync();
            else
                await connection.OpenAsync(token.Value);
            return new OracleDbConnection(connection);
        }
    }
}
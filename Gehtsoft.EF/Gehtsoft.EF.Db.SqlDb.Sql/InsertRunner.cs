using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class InsertRunner : SqlStatementRunner<SqlInsertStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;
        private SqlInsertStatement mInsert;
        private InsertQueryBuilder mInsertSimpleBuilder = null;
        private InsertSelectQueryBuilder mInsertSelectBuilder = null;

        internal InsertRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        internal InsertRunner(SqlCodeDomBuilder builder, SqlDbConnection connection)
        {
            mBuilder = builder;
            mConnection = connection;
        }

        protected override SqlStatement SqlStatement
        {
            get
            {
                return mInsert;
            }
        }

        protected override QueryWithWhereBuilder MainBuilder
        {
            get
            {
                return null;
            }
        }

        protected override SqlDbConnection Connection
        {
            get
            {
                return mConnection;
            }
        }

        protected override SqlCodeDomBuilder CodeDomBuilder
        {
            get
            {
                return mBuilder;
            }
        }

        internal override AQueryBuilder GetQueryBuilder(SqlInsertStatement insert)
        {
            if (mInsertSimpleBuilder == null && mInsertSelectBuilder == null)
            {
                mInsert = insert;
                if (mConnectionFactory != null)
                {
                    mConnection = mConnectionFactory.GetConnection();
                }
                try
                {
                    processInsert(insert);
                }
                finally
                {
                    if (mConnectionFactory != null)
                    {
                        if (mConnectionFactory.NeedDispose)
                            mConnection.Dispose();
                    }
                }
            }
            return (AQueryBuilder)mInsertSimpleBuilder ?? (AQueryBuilder)mInsertSelectBuilder;
        }

        private void processInsert(SqlInsertStatement insert)
        {
            Type entityType = mBuilder.EntityByName(insert.TableName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{insert.TableName}'"));
            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
            if (insert.Values != null)
            {
                mInsertSimpleBuilder = mConnection.GetInsertQueryBuilder(entityDescriptor.TableDescriptor);
                for (int i = 0; i < insert.Fields.Count; i++)
                {
                    SqlField field = insert.Fields[i];
                    SqlConstant constant = insert.Values[i];

                    string paramName = $"{entityDescriptor.TableDescriptor[field.Name].Name}";
                    BindParams.Add(paramName, constant.Value);
                }

                foreach (TableDescriptor.ColumnInfo column in entityDescriptor.TableDescriptor)
                {
                    if (insert.Fields.FindByName(column.ID) == null)
                    {
                        if (!column.PrimaryKey)
                        {
                            string paramName = $"{column.Name}";
                            if (column.DefaultValue != null)
                            {
                                BindParams.Add(paramName, column.DefaultValue);
                            }
                            else if (column.Nullable)
                            {
                                BindParams.Add(paramName, null);
                            }
                        }
                    }
                }
            }
            else if (insert.RightSelect != null)
            {
                SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection, this);
                SelectQueryBuilder selectBuilder = (SelectQueryBuilder)runner.GetQueryBuilder(insert.RightSelect);

                mInsertSelectBuilder = mConnection.GetInsertSelectQueryBuilder(entityDescriptor.TableDescriptor, selectBuilder);

                List<string> fieldNames = new List<string>();
                foreach (TableDescriptor.ColumnInfo column in entityDescriptor.TableDescriptor)
                {
                    if (insert.Fields.FindByName(column.ID) != null)
                    {
                        fieldNames.Add(column.Name);
                    }
                }

                mInsertSelectBuilder.IncludeOnly(fieldNames.ToArray());
            }
        }


        internal void RunWithResult(SqlInsertStatement insert)
        {
            mBuilder.BlockDescriptors.Peek().LastStatementResult = run(insert);
        }
        private object run(SqlInsertStatement insert)
        {
            List<object> result = new List<object>();
            mInsert = insert;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                processInsert(insert);

                using (SqlDbQuery query = mConnection.GetQuery(mInsertSimpleBuilder != null ? (AQueryBuilder)mInsertSimpleBuilder : (AQueryBuilder)mInsertSelectBuilder))
                {
                    ApplyBindParams(query);

                    query.ExecuteReader();
                    while (query.ReadNext())
                    {
                        object o = bindRecord(query);
                        result.Add(o);
                    }
                }
            }
            finally
            {
                if (mConnectionFactory != null)
                {
                    if (mConnectionFactory.NeedDispose)
                        mConnection.Dispose();
                }
            }
            return result;
        }

        private object bindRecord(SqlDbQuery query)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (query.FieldCount > 0) result.Add("LastInsertedId", query.GetValue(0));
            return result;
        }
    }
}

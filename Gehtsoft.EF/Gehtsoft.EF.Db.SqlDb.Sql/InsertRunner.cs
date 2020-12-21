using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
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

        TableDescriptor.ColumnInfo autoIncrement = null;
        private void processInsert(SqlInsertStatement insert)
        {
            Type entityType = mBuilder.EntityByName(insert.TableName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{insert.TableName}'"));
            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];

            foreach (TableDescriptor.ColumnInfo column in entityDescriptor.TableDescriptor)
            {
                if (column.Autoincrement == true && column.PrimaryKey == true)
                {
                    autoIncrement = column;
                    break;
                }
            }

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
        private dynamic run(SqlInsertStatement insert)
        {
            List<dynamic> result = new List<dynamic>();
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
                    bool executeNoData = false;
                    if (autoIncrement != null)
                    {
                        if (query.LanguageSpecifics.AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                        {
                            query.BindOutputParam(autoIncrement.Name, autoIncrement.DbType);
                            executeNoData = true;
                        }
                    }

                    if (executeNoData)
                    {
                        query.ExecuteNoData();
                        object v = query.GetParamValue(autoIncrement.Name, autoIncrement.PropertyAccessor.PropertyType);
                        if(v is Int32 || v is UInt32 || v is UInt64)
                        {
                            v = Convert.ChangeType(v, typeof(Int64));
                        }
                        var res = new ExpandoObject();
                        (res as IDictionary<string, object>).Add("LastInsertedId", v);
                        result.Add(res);
                    }
                    else
                    {
                        query.ExecuteReader();
                        while (query.ReadNext())
                        {
                            object o = bindRecord(query);
                            // MSSql Insert from Select returns IDs of all inserted records, but we need only the last one
                            if (result.Count > 0) 
                                result.Clear();
                            result.Add(o);
                        }
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
            var result = new ExpandoObject();
            IDictionary<string, object> _result = result as IDictionary<string, object>;
            object v = query.GetValue(0);
            if (v is Int32 || v is UInt32 || v is UInt64)
            {
                v = Convert.ChangeType(v, typeof(Int64));
            }
            if (query.FieldCount > 0) _result.Add("LastInsertedId", v);
            return result;
        }
    }
}

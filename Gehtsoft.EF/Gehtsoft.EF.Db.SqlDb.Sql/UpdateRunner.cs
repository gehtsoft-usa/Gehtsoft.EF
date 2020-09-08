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
    internal class UpdateRunner : StatementRunner<SqlUpdateStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;
        private SqlUpdateStatement mUpdate;
        private UpdateQueryBuilder mUpdateBuilder = null;

        internal UpdateRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        internal UpdateRunner(SqlCodeDomBuilder builder, SqlDbConnection connection)
        {
            mBuilder = builder;
            mConnection = connection;
        }

        protected override SqlStatement SqlStatement
        {
            get
            {
                return mUpdate;
            }
        }

        protected override QueryWithWhereBuilder MainBuilder
        {
            get
            {
                return mUpdateBuilder;
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

        public override AQueryBuilder GetQueryBuilder(SqlUpdateStatement insert)
        {
            if (mUpdateBuilder == null)
            {
                mUpdate = insert;
                if (mConnectionFactory != null)
                {
                    mConnection = mConnectionFactory.GetConnection();
                }
                try
                {
                    processUpdate(insert);
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
            return mUpdateBuilder;
        }

        private void processUpdate(SqlUpdateStatement insert)
        {
            Type entityType = mBuilder.EntityByName(insert.TableName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{insert.TableName}'"));
            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
            mUpdateBuilder = mConnection.GetUpdateQueryBuilder(entityDescriptor.TableDescriptor);

            for (int i = 0; i < insert.UpdateAssigns.Count; i++)
            {
                SqlUpdateAssign updateAssign = insert.UpdateAssigns[i];
                SqlField field = updateAssign.Field;

                TableDescriptor.ColumnInfo column = entityDescriptor.TableDescriptor[field.Name];

                if(updateAssign.Expression != null)
                {
                    if (updateAssign.Expression is SqlConstant constant)
                    {
                        string paramName = $"{entityDescriptor.TableDescriptor[field.Name].Name}";
                        BindParams.Add(paramName, constant.Value);
                        mUpdateBuilder.AddUpdateColumn(column, paramName);
                    }
                    else
                    {
                        mUpdateBuilder.AddUpdateColumnExpression(column, GetStrExpression(updateAssign.Expression), null);
                    }
                }
                else if(updateAssign.Select != null)
                {
                    SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection, this);
                    SelectQueryBuilder selectBuilder = (SelectQueryBuilder)runner.GetQueryBuilder(updateAssign.Select);

                    mUpdateBuilder.AddUpdateColumnSubquery(column, selectBuilder);
                }
            }

            if (insert.WhereClause != null)
            {
                mUpdateBuilder.Where.Add(LogOp.And, GetStrExpression(insert.WhereClause.RootExpression));
            }
        }

        public override object Run(SqlUpdateStatement insert)
        {
            List<object> result = new List<object>();
            mUpdate = insert;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                processUpdate(insert);

                using (SqlDbQuery query = mConnection.GetQuery(mUpdateBuilder))
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
            object result = null;
            if (query.FieldCount > 0) result = query.GetValue(0);
            return result;
        }
    }
}

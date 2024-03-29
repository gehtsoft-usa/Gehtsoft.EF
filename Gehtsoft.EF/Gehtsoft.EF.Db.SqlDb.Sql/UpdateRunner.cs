﻿using Gehtsoft.EF.Db.SqlDb.EntityQueries;
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
    internal class UpdateRunner : SqlStatementRunner<SqlUpdateStatement>
    {
        private readonly SqlCodeDomBuilder mBuilder;
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

        internal override AQueryBuilder GetQueryBuilder(SqlUpdateStatement statement)
        {
            if (mUpdateBuilder == null)
            {
                mUpdate = statement;
                if (mConnectionFactory != null)
                {
                    mConnection = mConnectionFactory.GetConnection();
                }
                try
                {
                    ProcessUpdate(statement);
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

        private void ProcessUpdate(SqlUpdateStatement update)
        {
            Type entityType = mBuilder.EntityByName(update.TableName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{update.TableName}'"));
            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
            mUpdateBuilder = mConnection.GetUpdateQueryBuilder(entityDescriptor.TableDescriptor);

            for (int i = 0; i < update.UpdateAssigns.Count; i++)
            {
                SqlUpdateAssign updateAssign = update.UpdateAssigns[i];
                SqlField field = updateAssign.Field;

                TableDescriptor.ColumnInfo column = entityDescriptor.TableDescriptor[field.Name];

                if (updateAssign.Expression != null)
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
                else if (updateAssign.Select != null)
                {
                    SelectRunner runner = new SelectRunner(CodeDomBuilder, Connection, this);
                    SelectQueryBuilder selectBuilder = (SelectQueryBuilder)runner.GetQueryBuilder(updateAssign.Select);

                    mUpdateBuilder.AddUpdateColumnSubquery(column, selectBuilder);
                }
            }

            if (update.WhereClause != null)
            {
                mUpdateBuilder.Where.Add(LogOp.And, GetStrExpression(update.WhereClause.RootExpression));
            }
        }

        internal void RunWithResult(SqlUpdateStatement update)
        {
            mBuilder.BlockDescriptors.Peek().LastStatementResult = Run(update);
        }

        private dynamic Run(SqlUpdateStatement update)
        {
            dynamic result = null;
            mUpdate = update;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                ProcessUpdate(update);

                using (SqlDbQuery query = mConnection.GetQuery(mUpdateBuilder))
                {
                    ApplyBindParams(query);

                    int updated = query.ExecuteNoData();
                    ExpandoObject subResult = new ExpandoObject();
                    (subResult as IDictionary<string, object>).Add("Updated", updated);
                    result = subResult;
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
    }
}

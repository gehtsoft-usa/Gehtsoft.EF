﻿using Gehtsoft.EF.Db.SqlDb.EntityQueries;
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
    internal class DeleteRunner : SqlStatementRunner<SqlDeleteStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;
        private SqlDeleteStatement mDelete;
        private DeleteQueryBuilder mDeleteBuilder = null;

        internal DeleteRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
        }

        internal DeleteRunner(SqlCodeDomBuilder builder, SqlDbConnection connection)
        {
            mBuilder = builder;
            mConnection = connection;
        }

        protected override SqlStatement SqlStatement
        {
            get
            {
                return mDelete;
            }
        }

        protected override QueryWithWhereBuilder MainBuilder
        {
            get
            {
                return mDeleteBuilder;
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

        internal override AQueryBuilder GetQueryBuilder(SqlDeleteStatement delete)
        {
            if (mDeleteBuilder == null)
            {
                mDelete = delete;
                if (mConnectionFactory != null)
                {
                    mConnection = mConnectionFactory.GetConnection();
                }
                try
                {
                    processDelete(delete);
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
            return mDeleteBuilder;
        }

        private void processDelete(SqlDeleteStatement delete)
        {
            Type entityType = mBuilder.EntityByName(delete.TableName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{delete.TableName}'"));
            EntityDescriptor entityDescriptor = AllEntities.Inst[entityType];
            mDeleteBuilder = mConnection.GetDeleteQueryBuilder(entityDescriptor.TableDescriptor);

            if (delete.WhereClause != null)
            {
                mDeleteBuilder.Where.Add(LogOp.And, GetStrExpression(delete.WhereClause.RootExpression));
            }
        }

        internal void RunWithResult(SqlDeleteStatement delete)
        {
            mBuilder.BlockDescriptors.Peek().LastStatementResult = run(delete);
        }

        private object run(SqlDeleteStatement delete)
        {
            List<object> result = new List<object>();
            mDelete = delete;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                processDelete(delete);

                using (SqlDbQuery query = mConnection.GetQuery(mDeleteBuilder))
                {
                    ApplyBindParams(query);
                    int deleted = query.ExecuteNoData();
                    Dictionary<string, object> subResult = new Dictionary<string, object>();
                    subResult.Add("Deleted", deleted);
                    result.Add(subResult);
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

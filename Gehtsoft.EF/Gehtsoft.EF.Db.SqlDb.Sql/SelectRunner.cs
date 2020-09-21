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
    internal class SelectRunner : SqlStatementRunner<SqlSelectStatement>
    {
        private SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;
        private SelectQueryBuilder mMainBuilder = null;
        private EntityDescriptor mMainEntityDescriptor = null;
        private SqlSelectStatement mSelect;

        internal SelectRunner(SqlCodeDomBuilder builder, ISqlDbConnectionFactory connectionFactory, IBindParamsOwner bindParamsOwner = null)
        {
            mBuilder = builder;
            mConnectionFactory = connectionFactory;
            BindParamsOwner = bindParamsOwner;
        }

        internal SelectRunner(SqlCodeDomBuilder builder, SqlDbConnection connection, IBindParamsOwner bindParamsOwner = null)
        {
            mBuilder = builder;
            mConnection = connection;
            BindParamsOwner = bindParamsOwner;
        }

        protected override SqlStatement SqlStatement
        {
            get
            {
                return mSelect;
            }
        }

        protected override QueryWithWhereBuilder MainBuilder
        {
            get
            {
                return mMainBuilder;
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

        public override AQueryBuilder GetQueryBuilder(SqlSelectStatement select)
        {
            if(MainBuilder == null)
            {
                mSelect = select;
                if (mConnectionFactory != null)
                {
                    mConnection = mConnectionFactory.GetConnection();
                }
                try
                {
                    processSelect(select);
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
            return MainBuilder;
        }

        private void processSelect(SqlSelectStatement select)
        {
            processFrom(select.FromClause);
            processSelectList(select.SelectList);
            reProcessFrom(select.FromClause);
            if (select.WhereClause != null) processWhereClause(select.WhereClause);
            if (select.Sorting != null) processSorting(select.Sorting);
            if (select.Grouping != null) processGrouping(select.Grouping);

            if (select.SetQuantifier == "DISTINCT")
                mMainBuilder.Distinct = true;

            mMainBuilder.Limit = mSelect.Limit;
            mMainBuilder.Skip = mSelect.Offset;
        }

        public void RunWithResult(SqlSelectStatement select)
        {
            mBuilder.BlockDescriptors.Peek().LastStatementResult = Run(select);
        }
        public override object Run(SqlSelectStatement select)
        {
            List<object> result = new List<object>();
            mSelect = select;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                processSelect(select);

                using (SqlDbQuery query = mConnection.GetQuery(mMainBuilder))
                {
                    ApplyBindParams(query);

                    query.ExecuteReader();
                    while (query.ReadNext())
                    {
                        object o = bindRecord(query, select);
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

        private void processGrouping(SqlGroupSpecificationCollection grouping)
        {
            foreach (SqlGroupSpecification group in grouping)
            {
                mMainBuilder.AddGroupByExpr(GetStrExpression(group.Expression));
            }
        }

        private void processSorting(SqlSortSpecificationCollection sorting)
        {
            foreach(SqlSortSpecification sort in sorting)
            {
                mMainBuilder.AddOrderByExpr(GetStrExpression(sort.Expression), sort.Ordering);
            }
        }

        private void processWhereClause(SqlWhereClause whereClause)
        {
            mMainBuilder.Where.Add(LogOp.And, GetStrExpression(whereClause.RootExpression));
        }

        private void processSelectList(SqlSelectList selectList)
        {
            if (!selectList.All)
            {
                foreach (SqlExpressionAlias item in selectList.FieldAliasCollection)
                {
                    bool isAggregate;
                    string sExpr = GetStrExpression(item.Expression, out isAggregate);
                    if (sExpr == null)
                        throw new SqlParserException(new SqlError(null, 0, 0, $"Unknown expression"));
                    mMainBuilder.AddExpressionToResultset(sExpr, GetDbType(item.Expression.RealType), isAggregate, item.Alias);
                }
            }
        }

        private void diveTableSpecification(SqlTableSpecification table)
        {
            if (table is SqlPrimaryTable primaryTable)
            {
                if (mMainBuilder == null)
                {
                    mMainBuilder = createBuilder(primaryTable.TableName, out mMainEntityDescriptor);
                }
                else
                {
                    mMainBuilder.AddTable(FindTableDescriptor(primaryTable.TableName), false);
                }
            }
            else if (table is SqlQualifiedJoinedTable joinedTable)
            {
                diveTableSpecification(joinedTable.LeftTable);

                TableJoinType joinType = TableJoinType.None;
                switch (joinedTable.JoinType)
                {
                    case "INNER":
                        joinType = TableJoinType.Inner;
                        break;
                    case "LEFT":
                        joinType = TableJoinType.Left;
                        break;
                    case "RIGHT":
                        joinType = TableJoinType.Right;
                        break;
                    case "FULL":
                        joinType = TableJoinType.Outer;
                        break;
                }

                joinedTable.BuilderEntity = mMainBuilder.AddTable(FindTableDescriptor(joinedTable.RightTable.TableName), null, joinType, null, null);
            }
            else if (table is SqlAutoJoinedTable autoJoinedTable)
            {
                diveTableSpecification(autoJoinedTable.LeftTable);

                mMainBuilder.AddTable(FindTableDescriptor(autoJoinedTable.RightTable.TableName), true);
            }
        }

        private void reDiveTableSpecification(SqlTableSpecification table)
        {
            if (table is SqlQualifiedJoinedTable joinedTable)
            {
                reDiveTableSpecification(joinedTable.LeftTable);

                joinedTable.TryExpression();
                joinedTable.BuilderEntity.On.Add(LogOp.And, GetStrExpression(joinedTable.JoinCondition));
            }
        }

        private void reProcessFrom(SqlFromClause fromClause)
        {
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                reDiveTableSpecification(table);
            }
        }

        private void processFrom(SqlFromClause fromClause)
        {
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                diveTableSpecification(table);
            }
        }

        private SelectQueryBuilder createBuilder(string entityName, out EntityDescriptor entityDescriptor)
        {
            Type entityType = mBuilder.EntityByName(entityName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{entityName}'"));
            entityDescriptor = AllEntities.Inst[entityType];
            return mConnection.GetSelectQueryBuilder(entityDescriptor.TableDescriptor);
        }

        private object bindRecord(SqlDbQuery query, SqlSelectStatement select)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            int fieldCount = query.FieldCount;

            for (int i = 0; i < fieldCount; i++)
            {
                string name = query.Field(i).Name;
                object value = query.GetValue(i);
                Type toType = null;

                SqlStatement.AliasEntry aliasEntry = select.AliasEntrys.Find(name);
                if (aliasEntry != null)
                {
                    toType = select.AliasEntrys.Find(name).Expression.RealType;
                }
                else
                {
                    foreach (SqlStatement.EntityEntry entityEntry in select.EntityEntrys)
                    {
                        EntityDescriptor entityDescriptor = entityEntry.EntityDescriptor;
                        name = mBuilder.NameByField(entityDescriptor.EntityType, name);
                        if (name != null)
                        {
                            toType = mBuilder.TypeByName(entityDescriptor.EntityType, name);
                            break;
                        }
                    }
                    if (name == null)
                    {
                        name = query.Field(i).Name;
                    }
                }

                if (value != null)
                {
                    if (value.GetType().FullName == "System.DBNull")
                    {
                        value = null;
                    }
                    else
                    {
                        if (toType != null)
                        {
                            value = query.LanguageSpecifics.TranslateValue(value, toType);
                        }
                    }
                }

                try
                {
                    result.Add(name, value);
                }
                catch
                {

                }
            }
            return result;
        }
    }
}

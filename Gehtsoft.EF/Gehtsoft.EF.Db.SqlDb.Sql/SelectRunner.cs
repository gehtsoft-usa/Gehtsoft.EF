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
    internal class SelectRunner : SqlStatementRunner<SqlSelectStatement>
    {
        private readonly SqlCodeDomBuilder mBuilder;
        private SqlDbConnection mConnection = null;
        private readonly ISqlDbConnectionFactory mConnectionFactory = null;
        private SelectQueryBuilder mMainBuilder = null;
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

        internal override AQueryBuilder GetQueryBuilder(SqlSelectStatement statement)
        {
            if (MainBuilder == null)
            {
                mSelect = statement;
                if (mConnectionFactory != null)
                {
                    mConnection = mConnectionFactory.GetConnection();
                }
                try
                {
                    ProcessSelect(statement);
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

        private void ProcessSelect(SqlSelectStatement select)
        {
            ProcessFrom(select.FromClause);
            ProcessSelectList(select.SelectList);
            ReProcessFrom(select.FromClause);
            if (select.WhereClause != null) ProcessWhereClause(select.WhereClause);
            if (select.Sorting != null) ProcessSorting(select.Sorting);
            if (select.Grouping != null) ProcessGrouping(select.Grouping);

            if (select.SetQuantifier == "DISTINCT")
                mMainBuilder.Distinct = true;

            mMainBuilder.Limit = mSelect.Limit;
            mMainBuilder.Skip = mSelect.Offset;
        }

        internal void RunWithResult(SqlSelectStatement select)
        {
            mBuilder.BlockDescriptors.Peek().LastStatementResult = Run(select);
        }
        internal dynamic Run(SqlSelectStatement select)
        {
            List<dynamic> result = new List<dynamic>();

            mSelect = select;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                ProcessSelect(select);

                using (SqlDbQuery query = mConnection.GetQuery(mMainBuilder))
                {
                    ApplyBindParams(query);

                    query.ExecuteReader();
                    while (query.ReadNext())
                    {
                        dynamic o = BindRecord(query, select);
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

        private SqlDbQuery mOpenedQuery = null;
        private Guid mQueryGuid = Guid.Empty;

        internal object ReadNext(SqlSelectStatement select)
        {
            if (mOpenedQuery != null && mOpenedQuery.ReadNext())
            {
                return BindRecord(mOpenedQuery, select);
            }
            return null;
        }

        internal void Open(SqlSelectStatement select)
        {
            mSelect = select;
            if (mConnectionFactory != null)
            {
                mConnection = mConnectionFactory.GetConnection();
            }
            try
            {
                ProcessSelect(select);

                mOpenedQuery = mConnection.GetQuery(mMainBuilder);
                ApplyBindParams(mOpenedQuery);

                mOpenedQuery.ExecuteReader();
                mQueryGuid = Guid.NewGuid();

                mBuilder.AddOpenedQuery(mQueryGuid, mOpenedQuery);
            }
            catch
            {
                if (mConnectionFactory != null)
                {
                    if (mConnectionFactory.NeedDispose)
                        mConnection.Dispose();
                }
            }
        }

        internal void Close()
        {
            if (mOpenedQuery != null)
            {
                mOpenedQuery.Dispose();
                mBuilder.RemoveOpenedQuery(mQueryGuid);
                mOpenedQuery = null;
                mQueryGuid = Guid.Empty;
            }
            mOpenedQuery = null;
            if (mConnectionFactory != null)
            {
                if (mConnectionFactory.NeedDispose)
                    mConnection.Dispose();
            }
        }

        private void ProcessGrouping(SqlGroupSpecificationCollection grouping)
        {
            foreach (SqlGroupSpecification group in grouping)
            {
                mMainBuilder.AddGroupByExpr(GetStrExpression(group.Expression));
            }
        }

        private void ProcessSorting(SqlSortSpecificationCollection sorting)
        {
            foreach (SqlSortSpecification sort in sorting)
            {
                mMainBuilder.AddOrderByExpr(GetStrExpression(sort.Expression), sort.Ordering);
            }
        }

        private void ProcessWhereClause(SqlWhereClause whereClause)
        {
            mMainBuilder.Where.Add(LogOp.And, GetStrExpression(whereClause.RootExpression));
        }

        private void ProcessSelectList(SqlSelectList selectList)
        {
            if (!selectList.All)
            {
                foreach (SqlExpressionAlias item in selectList.FieldAliasCollection)
                {
                    string sExpr = GetStrExpression(item.Expression, out bool isAggregate);
                    if (sExpr == null)
                        throw new SqlParserException(new SqlError(null, 0, 0, "Unknown expression"));
                    mMainBuilder.AddExpressionToResultset(sExpr, GetDbType(item.Expression.SystemType), isAggregate, item.Alias);
                }
            }
        }

        private void DiveTableSpecification(SqlTableSpecification table)
        {
            if (table is SqlPrimaryTable primaryTable)
            {
                if (mMainBuilder == null)
                {
                    mMainBuilder = CreateBuilder(primaryTable.TableName, out _);
                }
                else
                {
                    mMainBuilder.AddTable(FindTableDescriptor(primaryTable.TableName), false);
                }
            }
            else if (table is SqlQualifiedJoinedTable joinedTable)
            {
                DiveTableSpecification(joinedTable.LeftTable);

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
                DiveTableSpecification(autoJoinedTable.LeftTable);

                mMainBuilder.AddTable(FindTableDescriptor(autoJoinedTable.RightTable.TableName), true);
            }
        }

        private void ReDiveTableSpecification(SqlTableSpecification table)
        {
            if (table is SqlQualifiedJoinedTable joinedTable)
            {
                ReDiveTableSpecification(joinedTable.LeftTable);

                joinedTable.TryExpression();
                joinedTable.BuilderEntity.On.Add(LogOp.And, GetStrExpression(joinedTable.JoinCondition));
            }
        }

        private void ReProcessFrom(SqlFromClause fromClause)
        {
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                ReDiveTableSpecification(table);
            }
        }

        private void ProcessFrom(SqlFromClause fromClause)
        {
            foreach (SqlTableSpecification table in fromClause.TableCollection)
            {
                DiveTableSpecification(table);
            }
        }

        private SelectQueryBuilder CreateBuilder(string entityName, out EntityDescriptor entityDescriptor)
        {
            Type entityType = mBuilder.EntityByName(entityName);
            if (entityType == null)
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not found entity with name '{entityName}'"));
            entityDescriptor = AllEntities.Inst[entityType];
            return mConnection.GetSelectQueryBuilder(entityDescriptor.TableDescriptor);
        }

        private dynamic BindRecord(SqlDbQuery query, SqlSelectStatement select)
        {
            ExpandoObject result = new ExpandoObject();
            var _result = result as IDictionary<string, object>;
            int fieldCount = query.FieldCount;

            for (int i = 0; i < fieldCount; i++)
            {
                string name = query.Field(i).Name;
                object value = query.GetValue(i);
                Type toType = null;

                SqlStatement.AliasEntry aliasEntry = select.AliasEntrys.Find(name);
                if (aliasEntry != null)
                {
                    name = aliasEntry.AliasName;
                    toType = aliasEntry.Expression.SystemType;
                }
                else
                {
                    var key = name;
                    bool first = true;
                    foreach (SqlStatement.EntityEntry entityEntry in select.EntityEntrys)
                    {
                        EntityDescriptor entityDescriptor = entityEntry.EntityDescriptor;
                        name = mBuilder.NameByField(entityDescriptor.EntityType, key);
                        if (name != null)
                        {
                            if (!first)
                                name = entityEntry.EntityType.Name + "_" + name;
                            toType = mBuilder.TypeByName(entityDescriptor.EntityType, name);
                            break;
                        }
                        first = false;
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

                if (!_result.ContainsKey(name))
                    _result.Add(name, value);
            }
            return result;
        }
    }
}

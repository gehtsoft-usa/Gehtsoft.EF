using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// One item of the `SELECT` query resultset.
    /// </summary>
    public class SelectQueryBuilderResultsetItem
    {
        /// <summary>
        /// The raw SQL expression
        /// </summary>
        public string Expression { get; internal set; }

        /// <summary>
        /// Alias
        /// </summary>
        public string Alias { get; internal set; }

        /// <summary>
        /// The flag indicating wheter the expression is an aggregate expression
        /// </summary>
        public bool IsAggregate { get; internal set; }

        /// <summary>
        /// Column type.
        /// </summary>
        public DbType DbType { get; internal set; }

        internal SelectQueryBuilderResultsetItem(string expression, string alias, bool aggregate, DbType type)
        {
            Expression = expression;
            Alias = alias;
            IsAggregate = aggregate;
            DbType = type;
        }
    }

    /// <summary>
    /// The results of the `SELECT~ query.
    /// </summary>
    public class SelectQueryBuilderResultset : IEnumerable<SelectQueryBuilderResultsetItem>
    {
        private readonly List<SelectQueryBuilderResultsetItem> mList = new List<SelectQueryBuilderResultsetItem>();

        /// <summary>
        /// The number of columns in a resultset.
        /// </summary>
        public int Count => mList.Count;

        /// <summary>
        /// Gets column by its index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SelectQueryBuilderResultsetItem this[int index] => mList[index];

        [DocgenIgnore]
        public int AggregateCount { get; private set; } = 0;

        [DocgenIgnore]
        public IEnumerator<SelectQueryBuilderResultsetItem> GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        [DocgenIgnore]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Add(SelectQueryBuilderResultsetItem item)
        {
            if (item.IsAggregate)
                AggregateCount++;
            mList.Add(item);
        }
    }

    /// <summary>
    /// One element in `GROUP BY` or `ORDER BY` clause.
    /// </summary>
    public class SelectQueryBuilderByItem
    {
        /// <summary>
        /// The raw SQL expression.
        /// </summary>
        public string Expression { get; internal set; }

        /// <summary>
        /// The sorting direction.
        ///
        /// The sorting direction is ignored for `GROUP BY` clause.
        /// </summary>
        public SortDir Direction { get; internal set; }

        [DocgenIgnore]
        public SelectQueryBuilderByItem(string expression, SortDir direction = SortDir.Asc)
        {
            Expression = expression;
            Direction = direction;
        }
    }

    /// <summary>
    /// The list of the expressions for `GROUP BY` or `ORDER BY` clauses.
    /// </summary>
    public class SelectQueryBuilderByItemCollection : IEnumerable<SelectQueryBuilderByItem>
    {
        private readonly List<SelectQueryBuilderByItem> mList = new List<SelectQueryBuilderByItem>();

        /// <summary>
        /// Returns the number of the elements.
        /// </summary>
        public int Count => mList.Count;

        /// <summary>
        /// Returns element by its index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SelectQueryBuilderByItem this[int index] => mList[index];

        internal void Add(SelectQueryBuilderByItem item)
        {
            mList.Add(item);
        }

        [DocgenIgnore]
        public IEnumerator<SelectQueryBuilderByItem> GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        [DocgenIgnore]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// The query builder for `SELECT` command.
    ///
    /// Use <see cref="SqlDbConnection.GetInsertSelectQueryBuilder(TableDescriptor, SelectQueryBuilder, bool)"/> to create an instance of this object.
    /// </summary>
    public class SelectQueryBuilder : QueryWithWhereBuilder
    {
        [DocgenIgnore]
        internal protected SelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor mainTable) : base(specifics, mainTable)
        {
        }

        [DocgenIgnore]
        public new QueryBuilderEntity AddTable(TableDescriptor table, TableDescriptor.ColumnInfo connectingColumn, TableJoinType joinType, QueryBuilderEntity connectToTable, TableDescriptor.ColumnInfo connectToColumn) => base.AddTable(table, connectingColumn, joinType, connectToTable, connectToColumn);

        /// <summary>
        /// Adds a new table to the query.
        ///
        /// If auto-connection flag is set to `true` the table builder will try to connect the entity
        /// to the first entity which is related to this entity by having a foreign key to this entity
        /// or by being reference via foreign key from this entity.
        ///
        /// If the automatic connection does not product the desired result, please use disable auto-connection
        /// <see cref="QueryBuilderEntity.On"/> property of the returned object to configure the join condition.
        /// </summary>
        /// <param name="table">The table descriptor</param>
        /// <param name="autoConnect">The flag indicating whether the table needs to be auto-connected to the existing tables</param>
        /// <returns></returns>
        public new QueryBuilderEntity AddTable(TableDescriptor table, bool autoConnect = true) => base.AddTable(table, autoConnect);

        [DocgenIgnore]
        internal QueryBuilderEntity AddTable(TableDescriptor table, TableJoinType joinType) => base.AddTable(table, null, joinType, null, null);

        /// <summary>
        /// The flag indicating that the query must return only distinct resultset rows.
        /// </summary>
        public bool Distinct { get; set; } = false;
        public int Skip { get; set; } = 0;
        public int Limit { get; set; } = 0;

        protected SelectQueryBuilderResultset mResultset = new SelectQueryBuilderResultset();

        protected ConditionBuilder mHaving = null;

        public ConditionBuilder Having => mHaving ?? (mHaving = new ConditionBuilder(this));

        public SelectQueryBuilderResultset Resultset => mResultset;

        public void ResetResultset() => mResultset = new SelectQueryBuilderResultset();

        private int mColumnAlias = 0;

        public virtual void AddExpressionToResultset(string expression, DbType type, bool isAggregate = false, string alias = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (expression.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(expression));
                if (alias.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(alias));
            }

            if (alias == null)
                alias = $"column{++mColumnAlias}";
            mResultset.Add(new SelectQueryBuilderResultsetItem(expression, alias, isAggregate, type));
        }

        public virtual void AddToResultset(TableDescriptor.ColumnInfo column, QueryBuilderEntity entity, string alias = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (alias.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(alias));
            }
            mResultset.Add(new SelectQueryBuilderResultsetItem(GetAlias(column, entity), alias, false, column.DbType));
        }

        public virtual void AddToResultset(TableDescriptor.ColumnInfo column, string alias = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (alias.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(alias));
            }

            mResultset.Add(new SelectQueryBuilderResultsetItem(GetAlias(column, null), alias, false, column.DbType));
        }

        public virtual void AddToResultset(AggFn aggregate, TableDescriptor.ColumnInfo column, QueryBuilderEntity entity, string alias = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (alias.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(alias));
            }

            if (alias == null)
                alias = $"column{++mColumnAlias}";
            mResultset.Add(new SelectQueryBuilderResultsetItem(mSpecifics.GetAggFn(aggregate, GetAlias(column, entity)), alias, true, column.DbType));
        }

        public virtual void AddToResultset(AggFn aggregate, TableDescriptor.ColumnInfo column, string alias = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (alias.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(alias));
            }

            if (alias == null)
                alias = $"column{++mColumnAlias}";
            mResultset.Add(new SelectQueryBuilderResultsetItem(mSpecifics.GetAggFn(aggregate, GetAlias(column, null)), alias, true, column.DbType));
        }

        public virtual void AddToResultset(AggFn aggregate, string alias = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (alias.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(alias));
            }

            if (alias == null)
                alias = $"column{++mColumnAlias}";

            mResultset.Add(new SelectQueryBuilderResultsetItem(mSpecifics.GetAggFn(aggregate, null), alias, true, DbType.Int32));
        }

        public virtual void AddToResultset(TableDescriptor table, QueryBuilderEntity entity, string aliasPrefix = "")
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (aliasPrefix.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(aliasPrefix));
            }

            foreach (TableDescriptor.ColumnInfo column in table)
            {
                mResultset.Add(new SelectQueryBuilderResultsetItem(GetAlias(column, entity), aliasPrefix + column.Name, false, column.DbType));
            }
        }

        public virtual void AddToResultset(TableDescriptor table, string aliasPrefix = "")
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (aliasPrefix.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(aliasPrefix));
            }

            foreach (TableDescriptor.ColumnInfo column in table)
            {
                mResultset.Add(new SelectQueryBuilderResultsetItem(GetAlias(column, null), aliasPrefix + column.Name, false, column.DbType));
            }
        }

        protected SelectQueryBuilderByItemCollection mOrderBy = new SelectQueryBuilderByItemCollection();
        protected SelectQueryBuilderByItemCollection mGroupBy = new SelectQueryBuilderByItemCollection();

        public virtual void AddOrderBy(TableDescriptor.ColumnInfo column, SortDir direction = SortDir.Asc)
        {
            mOrderBy.Add(new SelectQueryBuilderByItem(GetAlias(column, null), direction));
        }

        public virtual void AddOrderBy(TableDescriptor.ColumnInfo column, QueryBuilderEntity entity, SortDir direction = SortDir.Asc)
        {
            mOrderBy.Add(new SelectQueryBuilderByItem(GetAlias(column, entity), direction));
        }

        public virtual void AddGroupBy(TableDescriptor.ColumnInfo column, QueryBuilderEntity entity)
        {
            mGroupBy.Add(new SelectQueryBuilderByItem(GetAlias(column, entity), SortDir.Asc));
        }

        protected internal void AddOrderByExpr(string expression, SortDir direction = SortDir.Asc)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (expression.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(expression));
            }
            mOrderBy.Add(new SelectQueryBuilderByItem(expression, direction));
        }

        protected internal void AddGroupByExpr(string expression)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
            {
                if (expression.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(expression));
            }

            mGroupBy.Add(new SelectQueryBuilderByItem(expression, SortDir.Asc));
        }

        public virtual void AddGroupBy(TableDescriptor.ColumnInfo column)
        {
            mGroupBy.Add(new SelectQueryBuilderByItem(GetAlias(column, null), SortDir.Asc));
        }

        public override void PrepareQuery()
        {
            StringBuilder query = PrepareSelectQueryCore();

            if (Limit > 0 && Skip > 0)
                query.Append(" LIMIT ").Append(Limit).Append(" OFFSET ").Append(Skip).Append(' ');
            else if (Limit > 0)
                query.Append(" LIMIT ").Append(Limit).Append(' ');
            else if (Skip > 0)
                query.Append(" OFFSET ").Append(Skip).Append(' ');
            mQuery = query.ToString();
        }

        public virtual StringBuilder PrepareSelectQueryCore()
        {
            StringBuilder query = new StringBuilder();

            query.Append("SELECT ");
            query.Append(BuildResultset());

            query.Append(" FROM ");
            query.Append(BuildFrom());

            Where.SetCurrentSignleConditionBuilder(null);
            if (!Where.IsEmpty)
            {
                query.Append(" WHERE ");
                query.Append(Where);
            }

            if (mGroupBy.Count > 0)
                query.Append(BuildGroupBy());

            Having.SetCurrentSignleConditionBuilder(null);
            if (!Having.IsEmpty)
            {
                query.Append(" HAVING ");
                query.Append(Having);
            }

            if (mOrderBy.Count > 0)
                query.Append(BuildOrderBy());

            return query;
        }

        internal SelectQueryBuilderResultsetItem ResultColumn(int index) => mResultset[index];

        protected virtual StringBuilder BuildResultset()
        {
            StringBuilder rs = new StringBuilder();

            if (mResultset.Count == 0)
            {
                rs.Append(" * ");
                return rs;
            }

            if (Distinct)
                rs.Append(" DISTINCT ");
            bool first = true;

            foreach (SelectQueryBuilderResultsetItem item in mResultset)
            {
                if (first)
                    first = false;
                else
                    rs.Append(", ");
                rs.Append(item.Expression);
                if (item.Alias != null)
                {
                    rs.Append(" AS ");
                    rs.Append(item.Alias);
                }
            }
            return rs;
        }

        protected virtual StringBuilder BuildFrom()
        {
            StringBuilder from = new StringBuilder();
            bool first = true;
            foreach (QueryBuilderEntity entity in mEntities)
            {
                if (first)
                    first = false;
                else
                {
                    switch (entity.JoinType)
                    {
                        case TableJoinType.None:
                            from.Append(", ");
                            break;

                        case TableJoinType.Inner:
                            from.Append(" INNER JOIN ");
                            break;

                        case TableJoinType.Left:
                            from.Append(" LEFT JOIN ");
                            break;

                        case TableJoinType.Right:
                            from.Append(mSpecifics.RightJoinSupported ? " RIGHT JOIN " : " JOIN ");
                            break;

                        case TableJoinType.Outer:
                            from.Append(mSpecifics.OuterJoinSupported ? " OUTER JOIN " : " JOIN ");
                            break;
                    }
                }
                from.Append(entity.Table.Name);
                from.Append(' ').Append(mSpecifics.TableAliasInSelect).Append(' ');
                from.Append(entity.Alias);
                if (entity.JoinType != TableJoinType.None)
                {
                    from.Append(" ON ");
                    from.Append(entity.On.ToString());
                }
            }
            return from;
        }

        protected virtual StringBuilder BuildOrderBy()
        {
            StringBuilder by = new StringBuilder();
            if (mOrderBy.Count > 0)
            {
                by.Append(" ORDER BY ");
                bool first = true;
                foreach (SelectQueryBuilderByItem item in mOrderBy)
                {
                    if (first)
                        first = false;
                    else
                        by.Append(", ");
                    by.Append(item.Expression);
                    if (item.Direction == SortDir.Desc)
                        by.Append(" DESC");
                }
            }
            return by;
        }

        protected virtual StringBuilder BuildGroupBy()
        {
            if (mSpecifics.AllNonAggregatesInGroupBy && mResultset.AggregateCount > 0)
            {
                if (mResultset.Count - mResultset.AggregateCount != mGroupBy.Count)
                {
                    foreach (SelectQueryBuilderResultsetItem item in mResultset)
                    {
                        if (!item.IsAggregate)
                        {
                            bool found = false;
                            foreach (SelectQueryBuilderByItem item1 in mGroupBy)
                            {
                                if (item1.Expression == item.Expression)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                mGroupBy.Add(new SelectQueryBuilderByItem(item.Expression));
                            }
                        }
                    }
                }
            }

            StringBuilder by = new StringBuilder();

            if (mGroupBy.Count > 0)
            {
                by.Append(" GROUP BY ");
                bool first = true;
                foreach (SelectQueryBuilderByItem item in mGroupBy)
                {
                    if (first)
                        first = false;
                    else
                        by.Append(", ");
                    by.Append(item.Expression);
                }
            }
            return by;
        }

        protected string mQuery;

        public override string Query
        {
            get { return mQuery; }
        }

        public override string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity)
        {
            if (queryEntity == null)
            {
                foreach (QueryBuilderEntity entity1 in mEntities)
                    if (entity1.Table == columnInfo.Table)
                    {
                        queryEntity = entity1;
                        break;
                    }
            }

            if (queryEntity == null)
                throw new EfSqlException(EfExceptionCode.NoTableInQuery);

            return $"{queryEntity.Alias}.{columnInfo.Name}";
        }

        protected TableDescriptor mQueryTableDescriptor;

        protected TableDescriptor GetTableDescriptor()
        {
            if (mQueryTableDescriptor == null)
            {
                if (Query == null)
                    PrepareQuery();
                mQueryTableDescriptor = new TableDescriptor($"({Query})");
                foreach (SelectQueryBuilderResultsetItem item in mResultset)
                    mQueryTableDescriptor.Add(new TableDescriptor.ColumnInfo() { Name = item.Alias, DbType = item.DbType });
            }
            return mQueryTableDescriptor;
        }

        public TableDescriptor QueryTableDescriptor => GetTableDescriptor();

        public override IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column) => new InQueryFieldReference(GetAlias(column, null));

        public virtual IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column, QueryBuilderEntity entity) => new InQueryFieldReference(GetAlias(column, entity));
    }

    [ExcludeFromCodeCoverage]
    [Obsolete("Use Having, Where and On properties of the query builder")]
    [DocgenIgnore]
    public static class SelectQueryWithWhereBuilderBackwardCompatibilityExtension
    {
        [Obsolete("Upgrade your code to using query Having property")]
        internal static void AddHavingP(this SelectQueryBuilder builder, string leftSide, CmpOp cmpOp, string rightSide) => builder.Having.Add(LogOp.And, leftSide, cmpOp, rightSide);

        [Obsolete("Upgrade your code to using query Having property")]
        internal static void AddHavingP(this SelectQueryBuilder builder, LogOp logOp, string leftSide, CmpOp cmpOp, string rightSide) => builder.Having.Add(logOp, leftSide, cmpOp, rightSide);

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string parameterName = null)
            => builder.Having.Add(logOp, builder.Having.PropertyName(entity, columnInfo), cmpOp, builder.Having.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string[] parameterNames)
            => builder.Having.Add(logOp, builder.Having.PropertyName(entity, columnInfo), cmpOp, builder.Having.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Having.Add(logOp, builder.Having.PropertyName(entity, columnInfo), cmpOp, builder.Having.Query(subquery));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string parameterName = null)
            => builder.Having.Add(logOp, builder.Having.PropertyName(aggFn, entity, columnInfo), cmpOp, builder.Having.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string[] parameterNames)
            => builder.Having.Add(logOp, builder.Having.PropertyName(aggFn, entity, columnInfo), cmpOp, builder.Having.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Having.Add(logOp, builder.Having.PropertyName(aggFn, entity, columnInfo), cmpOp, builder.Having.Query(subquery));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Having.Add(logOp, null, cmpOp, builder.Having.Query(subquery));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, LogOp logOp, SelectQueryBuilderResultsetItem rsItem, CmpOp cmpOp, string parameterName = null)
            => builder.Having.Add(logOp, rsItem.Expression, cmpOp, builder.Having.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Having property")]
        public static OpBracket AddHavingGroup(this SelectQueryBuilder builder, LogOp logOp) => builder.Having.AddGroup(logOp);

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string parameterName = null)
            => builder.Having.Add(LogOp.And, builder.Having.PropertyName(entity, columnInfo), cmpOp, builder.Having.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string[] parameterNames)
            => builder.Having.Add(LogOp.And, builder.Having.PropertyName(entity, columnInfo), cmpOp, builder.Having.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Having.Add(LogOp.And, builder.Having.PropertyName(entity, columnInfo), cmpOp, builder.Having.Query(subquery));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string parameterName = null)
            => builder.Having.Add(LogOp.And, builder.Having.PropertyName(aggFn, entity, columnInfo), cmpOp, builder.Having.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string[] parameterNames)
            => builder.Having.Add(LogOp.And, builder.Having.PropertyName(aggFn, entity, columnInfo), cmpOp, builder.Having.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, AggFn aggFn, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Having.Add(LogOp.And, builder.Having.PropertyName(aggFn, entity, columnInfo), cmpOp, builder.Having.Query(subquery));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Having.Add(LogOp.And, null, cmpOp, builder.Having.Query(subquery));

        [Obsolete("Upgrade your code to using query Having property")]
        public static void AddHaving(this SelectQueryBuilder builder, SelectQueryBuilderResultsetItem rsItem, CmpOp cmpOp, string parameterName = null)
            => builder.Having.Add(LogOp.And, rsItem.Expression, cmpOp, builder.Having.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Having property")]
        public static OpBracket AddHavingGroup(this SelectQueryBuilder builder) => builder.Having.AddGroup(LogOp.And);
    }

    public class ParameterGroupQueryBuilder : AQueryBuilder
    {
        private readonly StringBuilder mList = new StringBuilder();
        private readonly string mPrefix;

        public ParameterGroupQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
            mPrefix = mSpecifics.ParameterInQueryPrefix;
        }

        public void AddParameter(string parameter)
        {
            if (mList.Length > 0)
                mList.Append(", ");
            if (!string.IsNullOrEmpty(mPrefix) &&
                !parameter.StartsWith(mPrefix))
                mList.Append(mPrefix);
            mList.Append(parameter);
        }

        public override void PrepareQuery()
        {
        }

        public override string Query => mList.ToString();
    }
}
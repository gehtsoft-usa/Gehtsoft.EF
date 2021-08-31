using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The table used in the query.
    /// </summary>
    public class QueryBuilderEntity
    {
        internal Type EntityType { get; set; }
        internal SelectEntitiesQueryBase SelectEntitiesQuery { get; set; }

        /// <summary>
        /// The table.
        /// </summary>
        public TableDescriptor Table { get; internal set; }

        /// <summary>
        /// The table alias in the query.
        /// </summary>
        public string Alias { get; internal set; }

        /// <summary>
        /// How to table is connected.
        /// </summary>
        public TableJoinType JoinType { get; set; }

        /// <summary>
        /// The table to which this table is connected.
        /// </summary>
        public QueryBuilderEntity ConnectedToTable { get; set; }

        /// <summary>
        /// The field to which this table is connected.
        /// </summary>
        public TableDescriptor.ColumnInfo ConnectedToField { get; internal set; }

        /// <summary>
        /// The condition of connecting.
        /// </summary>
        public ConditionBuilder On { get; }

        [Obsolete("Use ConnectedTo properties instead")]
        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public string Expression
        {
            set
            {
                On.Add(LogOp.And, value);
            }
        }

        [Obsolete("Use On property instead")]
        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public void SetJoin(TableJoinType type, string expression)
        {
            JoinType = type;
            On.Add(LogOp.And, expression);
        }

        internal QueryBuilderEntity(IConditionBuilderInfoProvider infoProvider)
        {
            On = new ConditionBuilder(infoProvider);
        }
    }

    /// <summary>
    /// The collection of the entities in the query.
    /// </summary>
    public class QueryBuilderEntityCollection : IEnumerable<QueryBuilderEntity>
    {
        private readonly List<QueryBuilderEntity> mList = new List<QueryBuilderEntity>();

        /// <summary>
        /// The number of entities.
        /// </summary>
        public int Count => mList.Count;

        /// <summary>
        /// Gets entity by the index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public QueryBuilderEntity this[int index] => mList[index];

        /// <summary>
        /// Gets enumerator.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<QueryBuilderEntity> GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        [ExcludeFromCodeCoverage]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Add(QueryBuilderEntity entity)
        {
            mList.Add(entity);
        }
    }

    /// <summary>
    /// The base class for all builders with the condition.
    /// </summary>
    public abstract class QueryWithWhereBuilder : AQueryBuilder, IConditionBuilderInfoProvider
    {
        /// <summary>
        /// The language rules of the connection for which this query is created.
        /// </summary>
        public SqlDbLanguageSpecifics Specifics => mSpecifics;

        private static int gAlias = 0;
        private static readonly object gAliasMutex = new object();
        protected ConditionBuilder mWhere = null;

        /// <summary>
        /// The query condition.
        /// </summary>
        public ConditionBuilder Where => mWhere ?? (mWhere = new ConditionBuilder(this));

        protected static string NextAlias
        {
            get
            {
                lock (gAliasMutex)
                {
                    string rc = $"entity{gAlias}";
                    gAlias = (gAlias + 1) & 0xffff; //0...65536
                    return rc;
                }
            }
        }

        protected QueryWithWhereBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor mainTable) : base(specifics)
        {
            AddTable(mainTable, false);
        }

        protected QueryBuilderEntityCollection mEntities = new QueryBuilderEntityCollection();

        /// <summary>
        /// All entities in the query.
        /// </summary>
        public QueryBuilderEntityCollection Entities => mEntities;

        protected QueryBuilderEntity AddTable(TableDescriptor table, TableJoinType joinType, QueryBuilderEntity connectedToTable, TableDescriptor.ColumnInfo connectedToColumn)
        {
            string alias = NextAlias;
            QueryBuilderEntity rentity = new QueryBuilderEntity(this)
            {
                Table = table,
                Alias = alias,
                JoinType = joinType,
                ConnectedToTable = connectedToTable,
                ConnectedToField = connectedToColumn,
            };
            mEntities.Add(rentity);
            return rentity;
        }

        protected QueryBuilderEntity AddTable(TableDescriptor table, TableDescriptor.ColumnInfo connectingColumn, TableJoinType joinType, QueryBuilderEntity connectToTable, TableDescriptor.ColumnInfo connectToColumn)
        {
            QueryBuilderEntity entity = AddTable(table, joinType, connectToTable, connectToColumn);
            if (joinType != TableJoinType.None && connectingColumn != null && connectToTable != null && connectToColumn != null)
                entity.On.And().Raw($"{entity.Alias}.{connectingColumn.Name}").Eq().Raw($"{connectToTable.Alias}.{connectToColumn.Name}");
            return entity;
        }

        protected QueryBuilderEntity AddTable(TableDescriptor table, bool autoConnect = true)
        {
            TableDescriptor.ColumnInfo connectingColumn = null;
            TableJoinType joinType = TableJoinType.None;
            TableDescriptor.ColumnInfo connectToColumn = null;
            QueryBuilderEntity connectToTable = null;

            if (autoConnect)
            {
                //find a table that refers to the new added table
                //1) try to find other(fk)->(pk)this
                TableDescriptor.ColumnInfo pk = table.PrimaryKey;
                if (pk == null)
                    throw new EfSqlException(EfExceptionCode.NoPrimaryKeyInTable, table.Name);

                foreach (QueryBuilderEntity entity in mEntities)
                {
                    foreach (TableDescriptor.ColumnInfo column in entity.Table)
                    {
                        if (column.ForeignKey && column.ForeignTable == table)
                        {
                            connectingColumn = pk;
                            connectToTable = entity;
                            connectToColumn = column;
                            if (joinType == TableJoinType.None)
                                joinType = column.Nullable ? TableJoinType.Left : TableJoinType.Inner;
                        }
                    }
                }

                //2) try to find other(fk)->(this)pk
                if (connectToTable == null)
                {
                    foreach (QueryBuilderEntity entity in mEntities)
                    {
                        pk = entity.Table.PrimaryKey;
                        foreach (TableDescriptor.ColumnInfo column in table)
                        {
                            if (column.ForeignKey && column.ForeignTable == entity.Table)
                            {
                                connectingColumn = column;
                                connectToTable = entity;
                                connectToColumn = pk;
                                if (joinType == TableJoinType.None)
                                    joinType = column.Nullable ? TableJoinType.Right : TableJoinType.Inner;
                            }
                        }
                    }
                }

                if (connectToTable == null || connectToColumn == null)
                    throw new EfSqlException(EfExceptionCode.NoTableToConnect);
            }
            return AddTable(table, connectingColumn, joinType, connectToTable, connectToColumn);
        }

        /// <summary>
        /// Gets the alias of the column in the query.
        /// </summary>
        /// <param name="columnInfo"></param>
        /// <param name="queryEntity"></param>
        /// <returns></returns>
        public abstract string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity);

        /// <summary>
        /// Gets reference to a column of the specified query table.
        ///
        /// The reference is used when a sub-query condition need to have a reference to an entity in the main query.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column) => new InQueryFieldReference(GetAlias(column, null));

        /// <summary>
        /// Gets reference to a column of the specified query table.
        ///
        /// The reference is used when a sub-query condition need to have a reference to an entity in the main query.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="entity">The table involved into the query to which the column belongs to</param>
        /// <returns></returns>
        public IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column, QueryBuilderEntity entity) => new InQueryFieldReference(GetAlias(column, entity));
    }

    [ExcludeFromCodeCoverage]
    [Obsolete("Upgrade your code to using query Where property")]
    [DocgenIgnore]
    public static class QueryWithWhereBuilderBackwardCompatibilityExtension
    {
        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, CmpOp cmpOp, string parameterName = null)
            => builder.Where.Add(logOp, builder.Where.PropertyName(null, columnInfo), cmpOp, (parameterName?.Contains(".") ?? false) ? parameterName : builder.Where.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, CmpOp cmpOp, string[] parameterNames)
            => builder.Where.Add(logOp, builder.Where.PropertyName(null, columnInfo), cmpOp, builder.Where.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Where.Add(logOp, builder.Where.PropertyName(null, columnInfo), cmpOp, builder.Where.Query(subquery));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string parameterName = null)
            => builder.Where.Add(logOp, builder.Where.PropertyName(entity, columnInfo), cmpOp, (parameterName?.Contains(".") ?? false) ? parameterName : builder.Where.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string[] parameterNames)
            => builder.Where.Add(logOp, builder.Where.PropertyName(entity, columnInfo), cmpOp, builder.Where.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Where.Add(logOp, builder.Where.PropertyName(entity, columnInfo), cmpOp, builder.Where.Query(subquery));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, LogOp logOp, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Where.Add(logOp, null, cmpOp, builder.Where.Query(subquery));

        [Obsolete("Upgrade your code to using query Where property")]
        public static OpBracket AddWhereGroup(this QueryWithWhereBuilder builder, LogOp logOp) => builder.Where.AddGroup(logOp);

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, TableDescriptor.ColumnInfo columnInfo, CmpOp cmpOp, string parameterName = null)
            => builder.Where.Add(LogOp.And, builder.Where.PropertyName(null, columnInfo), cmpOp, (parameterName?.Contains(".") ?? false) ? parameterName : builder.Where.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, TableDescriptor.ColumnInfo columnInfo, CmpOp cmpOp, string[] parameterNames)
            => builder.Where.Add(LogOp.And, builder.Where.PropertyName(null, columnInfo), cmpOp, builder.Where.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, TableDescriptor.ColumnInfo columnInfo, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Where.Add(LogOp.And, builder.Where.PropertyName(null, columnInfo), cmpOp, builder.Where.Query(subquery));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string parameterName = null)
            => builder.Where.Add(LogOp.And, builder.Where.PropertyName(entity, columnInfo), cmpOp, (parameterName?.Contains(".") ?? false) ? parameterName : builder.Where.ParameterName(parameterName));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, string[] parameterNames)
            => builder.Where.Add(LogOp.And, builder.Where.PropertyName(entity, columnInfo), cmpOp, builder.Where.ParameterList(parameterNames));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity entity, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Where.Add(LogOp.And, builder.Where.PropertyName(entity, columnInfo), cmpOp, builder.Where.Query(subquery));

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereFilter(this QueryWithWhereBuilder builder, CmpOp cmpOp, AQueryBuilder subquery)
            => builder.Where.Add(LogOp.And, null, cmpOp, builder.Where.Query(subquery));

        [Obsolete("Upgrade your code to using query Where property")]
        public static OpBracket AddWhereGroup(this QueryWithWhereBuilder builder) => builder.Where.AddGroup(LogOp.And);

        [Obsolete("Upgrade your code to using query Where property")]
        public static void AddWhereExpression(this QueryWithWhereBuilder builder, LogOp logOp, string rawExpression) => builder.Where.Add(logOp, rawExpression);
    }

    /// <summary>
    /// The base class for queries with the filter where only one table is involved.
    /// </summary>
    public abstract class SingleTableQueryWithWhereBuilder : QueryWithWhereBuilder
    {
        protected TableDescriptor mDescriptor;
        protected string mQuery;

        /// <summary>
        /// Returns the query.
        ///
        /// Call <see cref="AQueryBuilder.PrepareQuery"/> before reading the query.
        /// </summary>
        public override string Query
        {
            get { return mQuery; }
        }

        protected SingleTableQueryWithWhereBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
            mQuery = null;
            mDescriptor = tableDescriptor;
        }

        /// <summary>
        /// Add where filter to find the main entity by the primary key value.
        ///
        /// The primary key value is expected in the parameter with the name of the primary key column.
        /// </summary>
        public void AddWhereFilterPrimaryKey()
        {
            if (mDescriptor.PrimaryKey == null)
                throw new InvalidOperationException("Table has no primary key");

            Where.Property(mDescriptor.PrimaryKey).Is(CmpOp.Eq).Parameter(mDescriptor.PrimaryKey.Name);
        }

        /// <summary>
        /// Gets the alias of the column in the query.
        /// </summary>
        /// <param name="columnInfo"></param>
        /// <param name="queryEntity"></param>
        /// <returns></returns>
        public override string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity)
        {
            if (queryEntity == null)
                queryEntity = mEntities[0];

            if (queryEntity != mEntities[0])
                throw new EfSqlException(EfExceptionCode.NoTableInQuery);

            return $"{columnInfo.Table.Name}.{columnInfo.Name}";
        }
    }
}

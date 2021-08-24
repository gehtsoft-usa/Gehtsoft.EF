using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class QueryBuilderEntity
    {
        internal Type EntityType { get; set; }
        internal SelectEntitiesQueryBase SelectEntitiesQuery { get; set; }

        public TableDescriptor Table { get; internal set; }
        public string Alias { get; internal set; }
        public TableJoinType JoinType { get; set; }
        public QueryBuilderEntity ConnectedToTable { get; set; }
        public TableDescriptor.ColumnInfo ConnectedToField { get; internal set; }
        public ConditionBuilder On { get; }

        [Obsolete("Use ConnectedTo properties instead")]
        public string Expression
        {
            set
            {
                On.Add(LogOp.And, value);
            }
        }

        [Obsolete("Use On property instead")]
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

    public class QueryBuilderEntityCollection : IEnumerable<QueryBuilderEntity>
    {
        private readonly List<QueryBuilderEntity> mList = new List<QueryBuilderEntity>();

        public int Count => mList.Count;

        public QueryBuilderEntity this[int index] => mList[index];

        public IEnumerator<QueryBuilderEntity> GetEnumerator()
        {
            return mList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Add(QueryBuilderEntity entity)
        {
            mList.Add(entity);
        }
    }

    public abstract class QueryWithWhereBuilder : AQueryBuilder, IConditionBuilderInfoProvider
    {
        public SqlDbLanguageSpecifics Specifics => mSpecifics;
        private static int gAlias = 0;
        private static readonly object gAliasMutex = new object();
        protected ConditionBuilder mWhere = null;
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

                if (connectToTable == null)
                    throw new EfSqlException(EfExceptionCode.NoTableToConnect);

                if (connectToColumn == null)
                {
                    //try to connect as other(fk)->this(pk)
                    pk = table.PrimaryKey;

                    if (pk == null)
                        throw new EfSqlException(EfExceptionCode.NoPrimaryKeyInTable, table.Name);

                    foreach (TableDescriptor.ColumnInfo column in connectToTable.Table)
                    {
                        if (column.ForeignKey && column.ForeignTable == table)
                        {
                            connectingColumn = pk;
                            connectToColumn = column;
                        }
                    }

                    //try to connect as other(pk)->this(fk)
                    if (connectToColumn == null)
                    {
                        pk = connectToTable.Table.PrimaryKey;
                        foreach (TableDescriptor.ColumnInfo column in table)
                        {
                            if (column.ForeignKey && column.ForeignTable == connectToTable.Table)
                            {
                                connectingColumn = pk;
                                connectToColumn = column;
                            }
                        }
                    }

                    if (connectToColumn == null)
                        throw new EfSqlException(EfExceptionCode.NoColumnToConnect);
                }
            }
            return AddTable(table, connectingColumn, joinType, connectToTable, connectToColumn);
        }

        public abstract string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity);
    }

    [Obsolete("Upgrade your code to using query Where property")]
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

    public abstract class SingleTableQueryWithWhereBuilder : QueryWithWhereBuilder
    {
        protected TableDescriptor mDescriptor;
        protected string mQuery;

        public override string Query
        {
            get { return mQuery; }
        }

        protected SingleTableQueryWithWhereBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
            mQuery = null;
            mDescriptor = tableDescriptor;
        }

        public void AddWhereFilterPrimaryKey()
        {
            if (mDescriptor.PrimaryKey == null)
                throw new InvalidOperationException("Table has no primary key");

            Where.Property(mDescriptor.PrimaryKey).Is(CmpOp.Eq).Parameter(mDescriptor.PrimaryKey.Name);
        }

        public override string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity)
        {
            if (queryEntity == null)
                queryEntity = mEntities[0];

            if (queryEntity != mEntities[0])
                throw new EfSqlException(EfExceptionCode.NoTableInQuery);

            return $"{columnInfo.Table.Name}.{columnInfo.Name}";
        }

        public override IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column) => new InQueryFieldReference(GetAlias(column, null));
    }
}

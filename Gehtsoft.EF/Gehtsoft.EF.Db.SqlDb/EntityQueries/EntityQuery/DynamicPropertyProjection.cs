using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Identifies a dynamic-property side-table join within a query: which entity (and which
    /// occurrence of it) owns the property, the property name, and the value type it is read as.
    ///
    /// The value type is part of the identity so the same property may be joined twice under two
    /// different types in one query.
    /// </summary>
    internal sealed class DynamicPropertyJoinKey : IEquatable<DynamicPropertyJoinKey>
    {
        public Type EntityType { get; }
        public string Name { get; }
        public int Occurrence { get; }
        public DynamicPropertyValueType ValueType { get; }

        public DynamicPropertyJoinKey(Type entityType, string name, int occurrence, DynamicPropertyValueType valueType)
        {
            EntityType = entityType;
            Name = name;
            Occurrence = occurrence;
            ValueType = valueType;
        }

        public bool Equals(DynamicPropertyJoinKey other)
            => other != null &&
               EntityType == other.EntityType &&
               Occurrence == other.Occurrence &&
               ValueType == other.ValueType &&
               string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as DynamicPropertyJoinKey);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (EntityType?.GetHashCode() ?? 0);
                hash = (hash * 31) + (Name?.GetHashCode() ?? 0);
                hash = (hash * 31) + Occurrence;
                hash = (hash * 31) + (int)ValueType;
                return hash;
            }
        }
    }

    /// <summary>
    /// One dynamic-property side-table join established in a <see cref="SelectEntitiesQueryBase"/>.
    ///
    /// A join is `LEFT JOIN &lt;table&gt;_props dp ON dp.owner = &lt;owner&gt;.&lt;pk&gt; AND dp.name = @p`.
    /// Its value column (`v_str` / `v_int` / `v_real`) is picked by the caller-supplied
    /// <see cref="DynamicPropertyValueType"/>, and <see cref="ColumnAlias"/> is the qualified
    /// expression (`dp.v_int`) that the resultset, ORDER BY, GROUP BY, HAVING and (optimized) WHERE
    /// clauses all reference.
    /// </summary>
    internal sealed class DynamicPropertyJoin
    {
        /// <summary>The side-table entity in the query builder (carries its alias).</summary>
        public QueryBuilderEntity Entity { get; }

        /// <summary>The value column the declared type targets.</summary>
        public TableDescriptor.ColumnInfo ValueColumn { get; }

        /// <summary>The declared value type (drives the value column and the read-time decode).</summary>
        public DynamicPropertyValueType ValueType { get; }

        /// <summary>The qualified column expression, e.g. `entity7.v_int`.</summary>
        public string ColumnAlias { get; }

        public DynamicPropertyJoin(QueryBuilderEntity entity, TableDescriptor.ColumnInfo valueColumn, DynamicPropertyValueType valueType, string columnAlias)
        {
            Entity = entity;
            ValueColumn = valueColumn;
            ValueType = valueType;
            ColumnAlias = columnAlias;
        }
    }

    /// <summary>
    /// Builds and caches the dynamic-property side-table joins of a <see cref="SelectEntitiesQueryBase"/>.
    /// </summary>
    internal static class DynamicPropertyProjection
    {
        /// <summary>
        /// Maps a value type to the identifier of the EAV value column that stores it.
        /// </summary>
        internal static string ColumnIdFor(DynamicPropertyValueType type)
        {
            switch (type)
            {
                case DynamicPropertyValueType.String:
                    return DynamicPropertiesTableBuilder.StringValueColumnId;
                case DynamicPropertyValueType.Real:
                    return DynamicPropertiesTableBuilder.RealValueColumnId;
                default:
                    // Integer, Long, Boolean, DateTime are all stored in v_int.
                    return DynamicPropertiesTableBuilder.IntValueColumnId;
            }
        }

        /// <summary>
        /// Ensures that the query has a join to the dynamic-property side table for the given
        /// property, occurrence and value type, creating it once and reusing it afterwards.
        ///
        /// Two joins that differ only by value type coexist (the type is part of the key), so the
        /// same property can be projected under two types in one query.
        /// </summary>
        internal static DynamicPropertyJoin EnsureJoin(SelectEntitiesQueryBase query, Type entityType, string name, int occurrence, DynamicPropertyValueType type)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            DynamicPropertyJoinKey key = new DynamicPropertyJoinKey(entityType, name, occurrence, type);
            if (query.DynamicPropertyJoins.TryGetValue(key, out DynamicPropertyJoin existing))
                return existing;

            EntityDescriptor descriptor = AllEntities.Get(entityType);
            TableDescriptor side = descriptor.DynamicPropertiesTable;
            if (side == null)
                throw new InvalidOperationException($"The entity '{entityType.FullName}' does not own a dynamic property set");

            QueryBuilderEntity owner = query.FindType(entityType, occurrence);
            if (owner == null)
                throw new ArgumentException($"The type '{entityType.FullName}' (occurrence {occurrence}) is not a part of the query", nameof(entityType));

            SelectQueryBuilder builder = query.SelectBuilder;

            // LEFT JOIN <table>_props dp ON dp.owner = <owner>.<pk>
            QueryBuilderEntity dp = builder.AddTable(side, side[DynamicPropertiesTableBuilder.OwnerColumnId], TableJoinType.Left, owner, descriptor.PrimaryKey);

            // ... AND dp.name = @name  (bound on the outer/executing query)
            string nameParameter = query.NextParam;
            query.Query.BindParam<string>(nameParameter, name);
            dp.On.And().Property(side[DynamicPropertiesTableBuilder.NameColumnId], dp).Eq().Parameter(nameParameter);

            TableDescriptor.ColumnInfo valueColumn = side[ColumnIdFor(type)];
            string columnAlias = builder.GetAlias(valueColumn, dp);

            DynamicPropertyJoin join = new DynamicPropertyJoin(dp, valueColumn, type, columnAlias);
            query.DynamicPropertyJoins.Add(key, join);
            return join;
        }
    }
}

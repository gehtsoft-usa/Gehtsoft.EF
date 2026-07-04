using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The intermediate builder returned by <see cref="EntityQueryConditionBuilderDynamicPropertiesExtension.DynamicPropertyOf{T}(EntityQueryConditionBuilder, string, int)"/>.
    ///
    /// It collects the comparison operator and value for a single dynamic-property predicate and
    /// then emits, into the underlying condition, one
    /// `owner-PK IN (SELECT owner FROM &lt;table&gt;_props WHERE name = @n AND &lt;value column&gt; {op} @v)`
    /// condition. Because the predicate is a self-contained sub-query, it composes with the ordinary
    /// And/Or/Not of the condition builder (an outer `AndNot`/`OrNot` becomes `NOT IN`).
    /// </summary>
    public sealed class DynamicPropertyConditionBuilder
    {
        private readonly SingleEntityQueryConditionBuilder mSingle;
        private readonly Type mEntityType;
        private readonly string mPropertyName;
        private readonly int mOccurrence;

        internal DynamicPropertyConditionBuilder(SingleEntityQueryConditionBuilder single, Type entityType, string propertyName, int occurrence)
        {
            mSingle = single;
            mEntityType = entityType;
            mPropertyName = propertyName;
            mOccurrence = occurrence;
        }

        /// <summary>Property value equals the value.</summary>
        public SingleEntityQueryConditionBuilder Eq(object value) => Apply(CmpOp.Eq, value);

        /// <summary>Property value does not equal the value (the property must be set).</summary>
        public SingleEntityQueryConditionBuilder Neq(object value) => Apply(CmpOp.Neq, value);

        /// <summary>Property value is greater than the value.</summary>
        public SingleEntityQueryConditionBuilder Gt(object value) => Apply(CmpOp.Gt, value);

        /// <summary>Property value is greater than or equal to the value.</summary>
        public SingleEntityQueryConditionBuilder Ge(object value) => Apply(CmpOp.Ge, value);

        /// <summary>Property value is less than the value.</summary>
        public SingleEntityQueryConditionBuilder Ls(object value) => Apply(CmpOp.Ls, value);

        /// <summary>Property value is less than or equal to the value.</summary>
        public SingleEntityQueryConditionBuilder Le(object value) => Apply(CmpOp.Le, value);

        /// <summary>Property value is like the pattern.</summary>
        public SingleEntityQueryConditionBuilder Like(string value) => Apply(CmpOp.Like, value);

        private SingleEntityQueryConditionBuilder Apply(CmpOp op, object value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            EntityDescriptor descriptor = AllEntities.Get(mEntityType);
            TableDescriptor side = descriptor.DynamicPropertiesTable;
            if (side == null)
                throw new InvalidOperationException($"The entity '{mEntityType.FullName}' does not own a dynamic property set");

            ConditionEntityQueryBase baseQuery = mSingle.Builder.BaseQuery;
            SqlDbConnection connection = baseQuery.Query.Connection;

            // Which value column the value lands in, and the encoded value to compare against.
            (DynamicPropertyValueType _, string columnName, object encoded) = DynamicPropertiesValueMapper.Encode(value);
            TableDescriptor.ColumnInfo valueColumn = side[ValueColumnId(columnName)];

            // Unique parameter names, bound on the OUTER (executing) query - the sub-query only
            // references them by name.
            string nameParameter = baseQuery.NextParam;
            string valueParameter = baseQuery.NextParam;
            baseQuery.Query.BindParam<string>(nameParameter, mPropertyName);
            baseQuery.Query.BindParam(valueParameter, valueColumn.DbType, encoded);

            // SELECT owner FROM <table>_props WHERE name = @name AND <value column> {op} @value
            SelectQueryBuilder subQuery = connection.GetSelectQueryBuilder(side);
            subQuery.AddToResultset(side[DynamicPropertiesTableBuilder.OwnerColumnId]);
            subQuery.Where.Property(side[DynamicPropertiesTableBuilder.NameColumnId]).Is(CmpOp.Eq).Parameter(nameParameter);
            subQuery.Where.Property(valueColumn).Is(op).Parameter(valueParameter);

            // <owner PK> IN (sub-query); the And/Or/Not the caller chose is carried by mSingle.
            return mSingle.PropertyOf(descriptor.PrimaryKey.ID, mEntityType, mOccurrence)
                          .Is(CmpOp.In)
                          .Query(subQuery);
        }

        private static string ValueColumnId(string columnName)
        {
            if (columnName == DynamicPropertiesTableBuilder.StringValueColumn)
                return DynamicPropertiesTableBuilder.StringValueColumnId;
            if (columnName == DynamicPropertiesTableBuilder.IntValueColumn)
                return DynamicPropertiesTableBuilder.IntValueColumnId;
            return DynamicPropertiesTableBuilder.RealValueColumnId;
        }
    }

    /// <summary>
    /// Extension methods that start a dynamic-property predicate in an entity query condition.
    /// </summary>
    public static class EntityQueryConditionBuilderDynamicPropertiesExtension
    {
        /// <summary>
        /// Starts a dynamic-property predicate, connected to the other conditions with logical and.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="builder"></param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public static DynamicPropertyConditionBuilder DynamicPropertyOf<T>(this EntityQueryConditionBuilder builder, string name, int occurrence = 0)
        {
            SingleEntityQueryConditionBuilder single = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            return new DynamicPropertyConditionBuilder(single, typeof(T), name, occurrence);
        }

        /// <summary>
        /// Continues a dynamic-property predicate on a condition started with `And`/`Or`/`AndNot`/`OrNot`.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="single"></param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public static DynamicPropertyConditionBuilder DynamicPropertyOf<T>(this SingleEntityQueryConditionBuilder single, string name, int occurrence = 0)
            => new DynamicPropertyConditionBuilder(single, typeof(T), name, occurrence);
    }
}

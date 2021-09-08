using System;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The query to update multiple entities by the condition.
    ///
    /// Use <see cref="EntityConnectionExtension.GetMultiDeleteEntityQuery(SqlDbConnection, Type)"/> to get
    /// an instance of this query.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class MultiUpdateEntityQuery : ConditionEntityQueryBase
    {
        internal readonly UpdateEntityQueryBuilder mUpdateBuilder;

        internal MultiUpdateEntityQuery(SqlDbQuery query, UpdateEntityQueryBuilder builder) : base(query, builder)
        {
            mUpdateBuilder = builder;
        }

        /// <summary>
        /// Add the value to set to all the records for the specified property.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public void AddUpdateColumn<T>(string propertyName, T value)
        {
            mUpdateBuilder.AddUpdateColumn(propertyName);
            mQuery.BindParam(propertyName, value);
        }

        /// <summary>
        /// Add the value to set to all the records for the specified property.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="rawExpression"></param>
        public void AddUpdateColumnByExpression(string propertyName, string rawExpression)
        {
            mUpdateBuilder.AddUpdateColumnByExpression(propertyName, rawExpression);
        }
    }
}
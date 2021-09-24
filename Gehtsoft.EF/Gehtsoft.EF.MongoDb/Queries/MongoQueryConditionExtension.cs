using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// Extensions methods for query conditions.
    ///
    /// See also <see cref="MongoQueryCondition"/>.
    /// </summary>
    public static class MongoQueryConditionExtension
    {
        /// <summary>
        /// Starts a condition and connects them to previous conditions using And operation.
        /// </summary>
        /// <param name="where"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder And(this MongoQueryCondition where) => where.Add(LogOp.And);

        /// <summary>
        /// Defines a group of conditions using the action and connects them to previous conditions using And operator.
        /// </summary>
        /// <param name="where"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static MongoQueryCondition And(this MongoQueryCondition where, Action<MongoQueryCondition> action)
        {
            where.AddGroup(LogOp.And, action);
            return where;
        }

        /// <summary>
        /// Starts a condition and connects them to previous conditions using Or operator.
        /// </summary>
        /// <param name="where"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Or(this MongoQueryCondition where) => where.Add(LogOp.Or);

        /// <summary>
        /// Defines a group of conditions using the action and connects them to previous conditions using Or operator.
        /// </summary>
        /// <param name="where"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static MongoQueryCondition Or(this MongoQueryCondition where, Action<MongoQueryCondition> action)
        {
            where.AddGroup(LogOp.Or, action);
            return where;
        }

        /// <summary>
        /// Starts a new condition with the property declaration and connects it to the previous condition using And operator.
        /// </summary>
        /// <param name="where"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public static MongoQuerySingleConditionBuilder Property(this MongoQueryCondition where, string property) => where.Add(LogOp.And).Property(property);
    }
}

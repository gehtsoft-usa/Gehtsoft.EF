using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The class is used to construct a filter condition for a mongo query.
    ///
    /// You can access this object using <see cref="MongoQueryWithCondition.Where"/> property.
    /// 
    /// Use <see cref="MongoQueryConditionExtension"/> to create easier to read conditions.
    /// </summary>
    public class MongoQueryCondition : IMongoConditionalQueryWhereTarget
    {
        private readonly BsonFilterExpressionBuilder mFilterBuilder;
        private readonly IMongoPathResolver mQuery;

        internal MongoQueryCondition(IMongoPathResolver query, BsonFilterExpressionBuilder filterBuilder)
        {
            mQuery = query;
            mFilterBuilder = filterBuilder;
        }

        /// <summary>
        /// Adds a condition and connects it to the other condition using the specified logical operation.
        /// 
        /// Please note that all conditions inside among top-level conditions or inside one group
        /// must be connected using the same logical operation. 
        /// 
        /// Please note that `LogOp.Not` is not currently supported.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        public MongoQuerySingleConditionBuilder Add(LogOp logOp) => new MongoQuerySingleConditionBuilder(mQuery, mFilterBuilder, logOp);

        /// <summary>
        /// Adds a group of conditions and connects it to other conditions using the specified logical operation.
        /// 
        /// All conditions, that will be added to the builder after this call and until the returned object is disposed,
        /// will be considered as conditions inside a group. 
        /// 
        /// You can think of this operation as about using a brackets.
        /// 
        /// See also <see cref="AddGroup(LogOp, Action{MongoQueryCondition})"/>.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        public IDisposable AddGroup(LogOp logOp)
        {
            mFilterBuilder.BeginGroup(logOp);
            return new MongoConditionalQueryWhereGroup(this);
        }

        /// <summary>
        /// Adds a group of conditions defined by an action and connects it to other conditions using the specified logical operation.
        /// 
        /// You can think of this operation as about using a brackets.
        /// </summary>
        /// <param name="logOp"></param>
        /// <param name="action"></param>
        public void AddGroup(LogOp logOp, Action<MongoQueryCondition> action)
        {
            using (var g = AddGroup(logOp))
                action(this);
        }

        void IMongoConditionalQueryWhereTarget.EndWhereGroup(MongoConditionalQueryWhereGroup group)
        {
            mFilterBuilder.EndGroup();
        }
    }
}

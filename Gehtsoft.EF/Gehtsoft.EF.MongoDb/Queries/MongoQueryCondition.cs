using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoQueryCondition : IMongoConditionalQueryWhereTarget
    {
        private readonly BsonFilterExpressionBuilder mFilterBuilder;
        private readonly IMongoPathResolver mQuery;

        internal MongoQueryCondition(IMongoPathResolver query, BsonFilterExpressionBuilder filterBuilder)
        {
            mQuery = query;
            mFilterBuilder = filterBuilder;
        }

        public MongoQuerySingleConditionBuilder Add(LogOp logOp) => new MongoQuerySingleConditionBuilder(mQuery, mFilterBuilder, logOp);

        public IDisposable AddGroup(LogOp logOp)
        {
            mFilterBuilder.BeginGroup(logOp);
            return new MongoConditionalQueryWhereGroup(this);
        }

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

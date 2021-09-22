using System;
using System.Diagnostics.CodeAnalysis;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    public abstract class MongoQueryWithCondition : MongoQuery
    {
        private protected BsonFilterExpressionBuilder FilterBuilder { get; private set; }

        public MongoQueryCondition Where { get; }

        protected MongoQueryWithCondition(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
            FilterBuilder = new BsonFilterExpressionBuilder();
            Where = new MongoQueryCondition(this, FilterBuilder);
        }

        protected void ResetFilter()
        {
            FilterBuilder = new BsonFilterExpressionBuilder();
        }
    }
}

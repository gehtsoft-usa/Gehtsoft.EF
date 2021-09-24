using System;
using System.Diagnostics.CodeAnalysis;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The base class for all queries with condition.
    /// </summary>
    public abstract class MongoQueryWithCondition : MongoQuery
    {
        private protected BsonFilterExpressionBuilder FilterBuilder { get; }

        /// <summary>
        /// The condition builder.
        /// </summary>
        public MongoQueryCondition Where { get; }

        protected MongoQueryWithCondition(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
            FilterBuilder = new BsonFilterExpressionBuilder();
            Where = new MongoQueryCondition(this, FilterBuilder);
        }
    }
}

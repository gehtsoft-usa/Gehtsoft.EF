using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The query to get a count of rows in the list.
    ///
    /// Use <see cref="MongoQueryWithCondition.Where"/> to define the condition.
    ///
    /// Use <see cref="MongoConnection.GetCountQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute()"/> or <see cref="MongoQuery.ExecuteAsync(CancellationToken?)"/>
    /// methods to execute this query.
    /// </summary>
    public class MongoCountQuery : MongoQueryWithCondition
    {
        private long? mRowCount = null;

        /// <summary>
        /// Returns the count of the rows.
        /// </summary>
        public long RowCount
        {
            get
            {
                if (mRowCount == null)
                    Execute();
                return mRowCount ?? 0;
            }
        }

        internal MongoCountQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        [DocgenIgnore]
        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            mRowCount = await Collection.CountDocumentsAsync(FilterBuilder.ToBsonDocument(), null, token ?? CancellationToken.None);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

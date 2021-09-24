using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The query to delete a group of entities defined by a condition.
    ///
    /// Use <see cref="MongoQueryWithCondition.Where"/> to define the condition.
    ///
    /// Use <see cref="MongoConnection.GetDeleteMultiEntityQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute()"/> or <see cref="MongoQuery.ExecuteAsync(CancellationToken?)"/> methods to execute this query.
    /// </summary>
    public class MongoDeleteMultiEntityQuery : MongoQueryWithCondition
    {
        internal MongoDeleteMultiEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        [DocgenIgnore]
        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            await Collection.DeleteManyAsync(FilterBuilder.ToBsonDocument(), token ?? CancellationToken.None);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

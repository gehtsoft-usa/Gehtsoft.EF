using Gehtsoft.EF.Utils;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The query to delete a list of entities.
    ///
    /// A list of queries is the object with the similar role as a table in SQL databases.
    ///
    /// Use <see cref="MongoConnection.GetDeleteListQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute()"/> or <see cref="MongoQuery.ExecuteAsync(CancellationToken?)"/> methods to execute this query.
    /// </summary>
    public class MongoDeleteListQuery : MongoQuery
    {
        internal MongoDeleteListQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        [DocgenIgnore]
        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            if (CollectionExists)
                await Connection.Database.DropCollectionAsync(CollectionName, token ?? CancellationToken.None);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

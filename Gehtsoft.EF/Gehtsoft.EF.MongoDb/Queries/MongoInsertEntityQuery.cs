using MongoDB.Bson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The query to insert an entity or an enumerable collection of entities into a list.
    ///
    /// Use <see cref="MongoConnection.GetInsertEntityQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute(object)"/> or <see cref="MongoQuery.ExecuteAsync(object, CancellationToken?)"/> to execute this query.
    /// </summary>
    public class MongoInsertEntityQuery : MongoQuery
    {
        internal MongoInsertEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }

        [DocgenIgnore]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null) => ExecuteAsyncCore(entity, token ?? CancellationToken.None);

        private async Task ExecuteAsyncCore(object entity, CancellationToken token)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.GetType() == Type)
            {
                entity.UpdateId(Description);
                await Collection.InsertOneAsync(entity.ConvertToBson(), null, token);
            }
            else if (entity is IEnumerable enumerable)
            {
                List<BsonDocument> docs = new List<BsonDocument>();
                foreach (object entity1 in enumerable)
                {
                    if (entity1 == null || entity1.GetType() != Type)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
                    entity1.UpdateId(Description);
                    docs.Add(entity1.ConvertToBson());
                }
                await Collection.InsertManyAsync(docs, null, token);
            }
            else
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
        }
    }
}

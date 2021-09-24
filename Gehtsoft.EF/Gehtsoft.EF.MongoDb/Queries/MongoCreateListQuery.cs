using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The query to create a list of entities.
    ///
    /// A list of queries is the object with the similar role as a table in SQL databases.
    ///
    /// Use <see cref="MongoConnection.GetCreateListQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute()"/> or <see cref="MongoQuery.ExecuteAsync(CancellationToken?)"/> methods to execute this query.
    /// </summary>
    public class MongoCreateListQuery : MongoQuery
    {
        internal MongoCreateListQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        [DocgenIgnore]
        public override Task ExecuteAsync(CancellationToken? token = null) => ExecuteAsyncCore(token ?? CancellationToken.None);

        [DocgenIgnore]
        private async Task ExecuteAsyncCore(CancellationToken token)
        {
            if (!(await Connection.Database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = new BsonDocument() { new BsonElement("name", new BsonString(CollectionName)) } })).Any())
            {
                await Connection.Database.CreateCollectionAsync(CollectionName, null, token);

                foreach (BsonEntityField field in Description.Fields)
                    if (field.IsSorted)
                        await Collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(field.Column, 1)), null, token);

                IEnumerable<MongoIndexAttribute> indexes = Type.GetTypeInfo().GetCustomAttributes<MongoIndexAttribute>();
                foreach (var attribute in indexes)
                    await Collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(TranslatePath(attribute.Key), 1)), null, token);
            }
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

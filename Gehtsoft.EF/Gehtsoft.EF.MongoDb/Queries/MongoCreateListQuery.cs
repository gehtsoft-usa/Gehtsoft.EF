using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoCreateListQuery : MongoQuery
    {
        internal MongoCreateListQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public override Task ExecuteAsync(CancellationToken? token = null) => ExecuteAsyncCore(token ?? CancellationToken.None);

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

        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

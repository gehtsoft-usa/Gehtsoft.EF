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

        public override Task ExecuteAsync() => ExecuteAsyncCore(null);
        public override Task ExecuteAsync(CancellationToken token) => ExecuteAsyncCore(token);

        private async Task ExecuteAsyncCore(CancellationToken? token)
        {
            if (!(await Connection.Database.ListCollectionNamesAsync(new ListCollectionNamesOptions {Filter = new BsonDocument() {new BsonElement("name", new BsonString(CollectionName))}})).Any())
            {
                if (token == null)
                    await Connection.Database.CreateCollectionAsync(CollectionName);
                else
                    await Connection.Database.CreateCollectionAsync(CollectionName, null, token.Value);

                foreach (BsonEntityField field in Description.Fields)
                    if (field.IsSorted)
                    {
                        if (token == null)
                            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(field.Column, 1)));
                        else
                            await Collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(field.Column, 1)), null, token.Value);
                    }

                IEnumerable<MongoIndexAttribute> indexes = Type.GetTypeInfo().GetCustomAttributes<MongoIndexAttribute>();
                foreach (var attribute in indexes)
                {
                    if (token == null)
                        await Collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(TranslatePath(attribute.Key), 1)));
                    else
                        await Collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocument(TranslatePath(attribute.Key), 1)), null, token.Value);
                }
            }
        }

        public override Task ExecuteAsync(object entity)
        {
            throw new InvalidOperationException();
        }
        public override Task ExecuteAsync(object entity, CancellationToken token)
        {
            throw new InvalidOperationException();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoUpdateMultiEntityQuery : MongoQueryWithCondition
    {
        private UpdateDefinition<BsonDocument> mUpdateDocument = null;

        public bool InsertIfNotExists { get; set; } = false;

        internal MongoUpdateMultiEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public void Set(string path, object value)
        {
            path = TranslatePath(path);
            BsonValue bvalue = EntityToBsonController.SerializeValue(value, null);
            if (mUpdateDocument == null)
                mUpdateDocument = Builders<BsonDocument>.Update.Set(path, bvalue);
            else
                mUpdateDocument.AddToSet(path, value);
        }

        public override async Task ExecuteAsync()
        {
            if (InsertIfNotExists)
                await Collection.UpdateManyAsync(FilterBuilder.ToBsonDocument(), mUpdateDocument, new UpdateOptions { IsUpsert = true });
            else
                await Collection.UpdateManyAsync(FilterBuilder.ToBsonDocument(), mUpdateDocument);
        }
        public override async Task ExecuteAsync(CancellationToken token)
        {
            if (InsertIfNotExists)
                await Collection.UpdateManyAsync(FilterBuilder.ToBsonDocument(), mUpdateDocument, new UpdateOptions { IsUpsert = true }, token);
            else
                await Collection.UpdateManyAsync(FilterBuilder.ToBsonDocument(), mUpdateDocument, null, token);
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

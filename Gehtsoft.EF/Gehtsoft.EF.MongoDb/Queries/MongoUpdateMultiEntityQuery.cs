using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
                throw new InvalidOperationException("Only one change allowed at a time");
        }

        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            UpdateOptions options = null;
            await Collection.UpdateManyAsync(FilterBuilder.ToBsonDocument(), mUpdateDocument, options, token ?? CancellationToken.None);
        }

        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

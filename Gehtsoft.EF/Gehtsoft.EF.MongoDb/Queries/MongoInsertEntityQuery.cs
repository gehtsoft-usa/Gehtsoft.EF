using MongoDB.Bson;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using System.Threading;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoInsertEntityQuery : MongoQuery
    {
        internal MongoInsertEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public override Task ExecuteAsync()
        {
            throw new InvalidOperationException();
        }
        public override Task ExecuteAsync(CancellationToken token)
        {
            throw new InvalidOperationException();
        }

        public override Task ExecuteAsync(object entity) => ExecuteAsyncCore(entity, null);

        public override Task ExecuteAsync(object entity, CancellationToken token) => ExecuteAsyncCore(entity, token);

        private async Task ExecuteAsyncCore(object entity, CancellationToken? token)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.GetType() == Type)
            {
                UpdateId(entity);
                if (token == null)
                    await Collection.InsertOneAsync(entity.ConvertToBson());
                else
                    await Collection.InsertOneAsync(entity.ConvertToBson(), null, token.Value);
            }
            else if (entity.GetType() == typeof(IEnumerable))
            {
                List<BsonDocument> docs = new List<BsonDocument>();
                foreach (object entity1 in (IEnumerable)entity)
                {
                    if (entity1 == null || entity1.GetType() != Type)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
                    UpdateId(entity1);
                    docs.Add(entity.ConvertToBson());
                }
                if (token == null)
                    await Collection.InsertManyAsync(docs);
                else
                    await Collection.InsertManyAsync(docs, null, token.Value);
            }
            else
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
        }

        private void UpdateId(object entity)
        {
            if (Description.PrimaryKey != null && Description.PrimaryKey.IsAutoId && Description.PrimaryKey.PropertyElementType == typeof(ObjectId) && Description.PrimaryKey.PropertyAccessor.GetValue(entity) == null)
                Description.PrimaryKey.PropertyAccessor.SetValue(entity, ObjectId.GenerateNewId());
        }
    }
}

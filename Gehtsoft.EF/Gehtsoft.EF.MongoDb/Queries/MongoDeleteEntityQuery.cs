using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoDeleteEntityQuery : MongoQuery
    {
        internal MongoDeleteEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
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
                BsonFilterExpressionBuilder filter = new BsonFilterExpressionBuilder();
                filter.Add(Description.PrimaryKey.Column, CmpOp.Eq, Description.PrimaryKey.PropertyAccessor.GetValue(entity));
                if (token == null)
                    await Collection.DeleteOneAsync(filter.ToBsonDocument());
                else
                    await Collection.DeleteOneAsync(filter.ToBsonDocument(), token.Value);
            }
            else if (entity.GetType() == typeof(IEnumerable))
            {
                List<BsonValue> ids = new List<BsonValue>();
                foreach (object entity1 in (IEnumerable)entity)
                {
                    if (entity1 == null || entity1.GetType() != Type)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
                    ids.Add(EntityToBsonController.SerializeValue(Description.PrimaryKey.PropertyAccessor.GetValue(entity), Description.PrimaryKey));
                }
                BsonFilterExpressionBuilder filter = new BsonFilterExpressionBuilder();
                filter.Add(Description.PrimaryKey.Column, CmpOp.In, ids);
                if (token == null)
                    await Collection.DeleteManyAsync(filter.ToBsonDocument());
                else
                    await Collection.DeleteManyAsync(filter.ToBsonDocument(), token.Value);
            }
            else
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
        }
    }
}

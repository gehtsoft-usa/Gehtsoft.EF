using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }

        public override Task ExecuteAsync(object entity, CancellationToken? token = null) => ExecuteAsyncCore(entity, token ?? CancellationToken.None);

        private async Task ExecuteAsyncCore(object entity, CancellationToken token)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entity.GetType() == Type)
            {
                BsonFilterExpressionBuilder filter = new BsonFilterExpressionBuilder();
                filter.Add(Description.PrimaryKey.Column, CmpOp.Eq, Description.PrimaryKey.PropertyAccessor.GetValue(entity));
                 await Collection.DeleteOneAsync(filter.ToBsonDocument(), token);
            }
            else if (entity is IEnumerable enumerable)
            {
                List<BsonValue> ids = new List<BsonValue>();
                foreach (object entity1 in enumerable)
                {
                    if (entity1 == null || entity1.GetType() != Type)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
                    ids.Add(EntityToBsonController.SerializeValue(Description.PrimaryKey.PropertyAccessor.GetValue(entity1), Description.PrimaryKey));
                }
                BsonFilterExpressionBuilder filter = new BsonFilterExpressionBuilder();
                filter.Add(Description.PrimaryKey.Column, CmpOp.In, ids);
                await Collection.DeleteManyAsync(filter.ToBsonDocument(), token);
            }
            else
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
        }
    }
}

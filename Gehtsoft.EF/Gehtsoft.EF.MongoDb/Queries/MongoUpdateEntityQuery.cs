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
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoUpdateEntityQuery : MongoQueryWithCondition
    {
        internal MongoUpdateEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public bool InsertIfNotExists { get; set; } = false;

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
                entity.UpdateId(Description);
                FilterDefinition<BsonDocument> filter;
                if (FilterBuilder.IsEmpty)
                {
                    BsonFilterExpressionBuilder filterBuilder = new BsonFilterExpressionBuilder();
                    filterBuilder.Add(Description.PrimaryKey.Column, CmpOp.Eq, Description.PrimaryKey.PropertyAccessor.GetValue(entity));
                    filter = filterBuilder.ToBsonDocument();
                }
                else
                {
                    filter = FilterBuilder.ToBsonDocument();
                }
                if (InsertIfNotExists)
                    await Collection.ReplaceOneAsync(filter, entity.ConvertToBson(), new ReplaceOptions { IsUpsert = true }, token);
                else
                    await Collection.ReplaceOneAsync(filter, entity.ConvertToBson(), new ReplaceOptions { IsUpsert = false }, token);
            }
            else if (entity is IEnumerable enumerable)
            {
                foreach (object entity1 in enumerable)
                {
                    if (entity1 == null || entity1.GetType() != Type)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
                    await ExecuteAsyncCore(entity1, token);
                }
            }
            else
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
        }
    }
}
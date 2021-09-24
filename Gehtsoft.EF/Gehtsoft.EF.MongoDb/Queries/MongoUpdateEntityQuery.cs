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
using Gehtsoft.EF.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The query to update an entity or an enumerable collection of entities.
    ///
    /// Use <see cref="MongoConnection.GetUpdateEntityQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute(object)"/> or <see cref="MongoQuery.ExecuteAsync(object, CancellationToken?)"/> to execute this query.
    /// </summary>
    public class MongoUpdateEntityQuery : MongoQueryWithCondition
    {
        internal MongoUpdateEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        /// <summary>
        /// Gets or sets a flag indicating whether the entity that does not exist must be inserted.
        ///
        /// Set the flag to `true` to insert the entities if they don't exist.
        ///
        /// Set the flag to `false` to ignore the entities that don't exists in the list.
        /// </summary>
        public bool InsertIfNotExists { get; set; } = false;

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }

        [DocgenIgnore]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null) => ExecuteAsyncCore(entity, token ?? CancellationToken.None);

        [DocgenIgnore]
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
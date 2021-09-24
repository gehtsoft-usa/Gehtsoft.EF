using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    /// The query to update one property of a group of entities defined by a condition.
    ///
    /// Use <see cref="MongoQueryWithCondition.Where"/> to define the condition.
    ///
    /// Use <see cref="MongoConnection.GetUpdateMultiEntityQuery{T}"/> to get the query object.
    ///
    /// Use <see cref="MongoQuery.Execute()"/> or <see cref="MongoQuery.ExecuteAsync(CancellationToken?)"/> methods to execute this query.
    /// </summary>
    public class MongoUpdateMultiEntityQuery : MongoQueryWithCondition
    {
        private UpdateDefinition<BsonDocument> mUpdateDocument = null;

        internal MongoUpdateMultiEntityQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        /// <summary>
        /// Sets the property to change.
        /// </summary>
        /// <param name="path">See [link=mongopath]Paths[/link] article for details</param>
        /// <param name="value"></param>
        public void Set(string path, object value)
        {
            path = TranslatePath(path);
            BsonValue bvalue = EntityToBsonController.SerializeValue(value, null);
            if (mUpdateDocument == null)
                mUpdateDocument = Builders<BsonDocument>.Update.Set(path, bvalue);
            else
                throw new InvalidOperationException("Only one change allowed at a time");
        }

        [DocgenIgnore]
        public override async Task ExecuteAsync(CancellationToken? token = null)
        {
            UpdateOptions options = null;
            await Collection.UpdateManyAsync(FilterBuilder.ToBsonDocument(), mUpdateDocument, options, token ?? CancellationToken.None);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public override Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            throw new InvalidOperationException();
        }
    }
}

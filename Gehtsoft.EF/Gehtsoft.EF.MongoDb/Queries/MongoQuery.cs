using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The base class for mongo queries
    /// </summary>
    public abstract class MongoQuery : IDisposable, IMongoPathResolver
    {
        protected Type Type { get; }

        protected BsonEntityDescription Description { get; }

        protected string CollectionName => Description.Table ?? Description.EntityType.Name;

        private IMongoCollection<BsonDocument> mCollection = null;

        /// <summary>
        /// The underlying Mongo collection object.
        /// </summary>
        public IMongoCollection<BsonDocument> Collection => mCollection ?? (mCollection = Connection.Database.GetCollection<BsonDocument>(CollectionName));

        private readonly PathTranslator mPathTranslator;

        protected bool CollectionExists
        {
            get
            {
                BsonDocument filter = new BsonDocument("name", Description.Table ?? Description.EntityType.Name);
                IAsyncCursor<BsonDocument> list = Connection.Database.ListCollections(new ListCollectionsOptions() { Filter = filter });
                return list.Any();
            }
        }

        /// <summary>
        /// The connection which is used to created this query.
        /// </summary>
        public MongoConnection Connection { get; }

        /// <summary>
        /// The type of the entity associated with the list.
        /// </summary>
        public Type EntityType => Type;

        protected MongoQuery(MongoConnection connection, Type entityType)
        {
            Connection = connection;
            Type = entityType;
            Description = AllEntities.Inst.FindBsonEntity(entityType);
            mPathTranslator = new PathTranslator(entityType, Description);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        ~MongoQuery()
        {
            Dispose(false);
        }

        [DocgenIgnore]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // nothing to dispose in MongoDB
        }

        /// <summary>
        /// Executes the query if the query is not associated with a particular object (async version).
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public abstract Task ExecuteAsync(CancellationToken? token = null);

        /// <summary>
        /// Executes the query if the query is associated with a particular object or an array of objects (async version).
        /// </summary>
        /// <param name="entity">The entity object or an enumerable collection or array of objects</param>
        /// <param name="token"></param>
        /// <returns></returns>
        public abstract Task ExecuteAsync(object entity, CancellationToken? token = null);

        /// <summary>
        /// Executes the query if the query is not associated with a particular object.
        /// </summary>
        public void Execute() => ExecuteAsync().Wait();

        /// <summary>
        /// Executes the query if the query is associated with a particular object or an array of objects.
        /// </summary>
        /// <param name="entity">The entity object or an enumerable collection or array of objects</param>
        public void Execute(object entity) => ExecuteAsync(entity).Wait();

        string IMongoPathResolver.TranslatePath(string path) => mPathTranslator.TranslatePath(path);

        private protected string TranslatePath(string path) => mPathTranslator.TranslatePath(path);
    }
}

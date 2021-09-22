using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    public abstract class MongoQuery : IDisposable, IMongoPathResolver
    {
        protected Type Type { get; }

        protected BsonEntityDescription Description { get; }

        protected string CollectionName => Description.Table ?? Description.EntityType.Name;

        private IMongoCollection<BsonDocument> mCollection = null;

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

        public MongoConnection Connection { get; }

        public Type EntityType => Type;

        protected MongoQuery(MongoConnection connection, Type entityType)
        {
            Connection = connection;
            Type = entityType;
            Description = AllEntities.Inst.FindBsonEntity(entityType);
            mPathTranslator = new PathTranslator(entityType, Description);
        }

        ~MongoQuery()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // nothing to dispose in MongoDB
        }

        public abstract Task ExecuteAsync(CancellationToken? token = null);

        public abstract Task ExecuteAsync(object entity, CancellationToken? token = null);

        public void Execute() => ExecuteAsync().Wait();

        public void Execute(object entity) => ExecuteAsync(entity).Wait();

        string IMongoPathResolver.TranslatePath(string path) => mPathTranslator.TranslatePath(path);

        private protected string TranslatePath(string path) => mPathTranslator.TranslatePath(path);
    }
}

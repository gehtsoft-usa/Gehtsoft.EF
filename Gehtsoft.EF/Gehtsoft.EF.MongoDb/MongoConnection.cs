using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    public partial class MongoConnection : IDisposable
    {
        private IMongoDatabase mDatabase;

        public IMongoDatabase Database => mDatabase;

        internal MongoConnection(IMongoDatabase db)
        {
            mDatabase = db;
        }

        public string[] GetSchema()
        {
            List<string> schema = new List<string>();
            IAsyncCursor<BsonDocument> list = mDatabase.ListCollections(new ListCollectionsOptions() { });
            while (list.MoveNext())
            {
                foreach (BsonDocument doc in list.Current)
                    schema.Add(doc["name"].AsString);
            }
            return schema.ToArray();
        }

        public void Dispose()
        {

        }

        public MongoCreateListQuery GetCreateListQuery<T>() => new MongoCreateListQuery(this, typeof(T));
        public MongoDeleteListQuery GetDeleteListQuery<T>() => new MongoDeleteListQuery(this, typeof(T));
        public MongoInsertEntityQuery GetInsertEntityQuery<T>() => new MongoInsertEntityQuery(this, typeof(T));
        public MongoUpdateEntityQuery GetUpdateEntityQuery<T>() => new MongoUpdateEntityQuery(this, typeof(T));
        public MongoUpdateMultiEntityQuery GetUpdateMultiEntityQuery<T>() => new MongoUpdateMultiEntityQuery(this, typeof(T));
        public MongoDeleteEntityQuery GetDeleteEntityQuery<T>() => new MongoDeleteEntityQuery(this, typeof(T));
        public MongoDeleteMultiEntityQuery GetDeleteMultiEntityQuery<T>() => new MongoDeleteMultiEntityQuery(this, typeof(T));
        public MongoSelectQuery GetSelectQuery<T>(bool expandReference = false) => new MongoSelectQuery(this, typeof(T), expandReference);
        public MongoCountQuery GetCountQuery<T>() => new MongoCountQuery(this, typeof(T));

        public MongoCreateListQuery GetCreateListQuery(Type t) => new MongoCreateListQuery(this, t);
        public MongoDeleteListQuery GetDeleteListQuery(Type t) => new MongoDeleteListQuery(this, t);
        public MongoInsertEntityQuery GetInsertEntityQuery(Type t) => new MongoInsertEntityQuery(this, t);
        public MongoUpdateEntityQuery GetUpdateEntityQuery(Type t) => new MongoUpdateEntityQuery(this, t);
        public MongoUpdateMultiEntityQuery GetUpdateMultiEntityQuery(Type t) => new MongoUpdateMultiEntityQuery(this, t);
        public MongoDeleteEntityQuery GetDeleteEntityQuery(Type t) => new MongoDeleteEntityQuery(this, t);
        public MongoDeleteMultiEntityQuery GetDeleteMultiEntityQuery(Type t) => new MongoDeleteMultiEntityQuery(this, t);
        public MongoSelectQuery GetSelectQuery(Type t, bool expandReference = false) => new MongoSelectQuery(this, t, expandReference);

        public MongoCountQuery GetCountQuery(Type t) => new MongoCountQuery(this, t);
    }

    public static class MongoConnectionFactory
    {
        public static MongoConnection Create(string connectionString)
        {
            MongoUrl url = MongoUrl.Create(connectionString);
            IMongoClient client = new MongoClient(url);
            IMongoDatabase database = client.GetDatabase(url.DatabaseName);
            return new MongoConnection(database);
        }

        public static Task<MongoConnection> CreateAsync(string connectionString)
        {
            MongoUrl url = MongoUrl.Create(connectionString);
            IMongoClient client = new MongoClient(url);
            IMongoDatabase database = client.GetDatabase(url.DatabaseName);
            return Task.FromResult(new MongoConnection(database));

        }

    }
}

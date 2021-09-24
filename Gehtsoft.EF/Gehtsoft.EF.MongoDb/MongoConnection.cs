using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The connection class for mongo database.
    /// </summary>
    public partial class MongoConnection : IDisposable
    {
        private readonly IMongoDatabase mDatabase;

        /// <summary>
        /// The underlying MongoDB connection object.
        /// </summary>
        public IMongoDatabase Database => mDatabase;

        internal MongoConnection(IMongoDatabase db)
        {
            mDatabase = db;
        }

        /// <summary>
        /// Returns the names of all collections.
        /// </summary>
        /// <returns></returns>
        public string[] GetSchema() => GetSchemaAsync().ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>
        /// Returns the names of all collections (asynchronous version).
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> GetSchemaAsync()
        {
            List<string> schema = new List<string>();
            IAsyncCursor<BsonDocument> list = await mDatabase.ListCollectionsAsync(new ListCollectionsOptions());
            while (await list.MoveNextAsync())
            {
                foreach (BsonDocument doc in list.Current)
                    schema.Add(doc["name"].AsString);
            }
            return schema.ToArray();
        }

        [DocgenIgnore]
        public void Dispose()
        {
            // Nothing to dispose for MongoDB
        }

        /// <summary>
        /// Gets a query to create a list.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoCreateListQuery GetCreateListQuery<T>() => new MongoCreateListQuery(this, typeof(T));

        /// <summary>
        /// Gets a query to delete a list.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoDeleteListQuery GetDeleteListQuery<T>() => new MongoDeleteListQuery(this, typeof(T));

        /// <summary>
        /// Gets a query to insert an entity or an array of entities into the list.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoInsertEntityQuery GetInsertEntityQuery<T>() => new MongoInsertEntityQuery(this, typeof(T));

        /// <summary>
        /// Gets a query to update an entitiy or an array of entities in the list.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoUpdateEntityQuery GetUpdateEntityQuery<T>() => new MongoUpdateEntityQuery(this, typeof(T));

        /// <summary>
        /// Gets a query to update one property of a group of entities defined by a condition.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoUpdateMultiEntityQuery GetUpdateMultiEntityQuery<T>() => new MongoUpdateMultiEntityQuery(this, typeof(T));

        /// <summary>
        /// Gets a query to delete an entity or an array of the entities.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoDeleteEntityQuery GetDeleteEntityQuery<T>() => new MongoDeleteEntityQuery(this, typeof(T));

        /// <summary>
        /// Gets a query to delete an group entities defined by a condition.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoDeleteMultiEntityQuery GetDeleteMultiEntityQuery<T>() => new MongoDeleteMultiEntityQuery(this, typeof(T));

        /// <summary>
        /// Gets a query that selects an entities from the list.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expandReference">The parameter has no use now and reserved for future implementation of joined projections</param>
        /// <returns></returns>
        public MongoSelectQuery GetSelectQuery<T>(bool expandReference = false) => new MongoSelectQuery(this, typeof(T), expandReference);

        /// <summary>
        /// Gets a query that selects a count of entities from the list.
        ///
        /// The query must be disposed after use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public MongoCountQuery GetCountQuery<T>() => new MongoCountQuery(this, typeof(T));

        [DocgenIgnore]
        public MongoCreateListQuery GetCreateListQuery(Type t) => new MongoCreateListQuery(this, t);
        [DocgenIgnore]
        public MongoDeleteListQuery GetDeleteListQuery(Type t) => new MongoDeleteListQuery(this, t);
        [DocgenIgnore]
        public MongoInsertEntityQuery GetInsertEntityQuery(Type t) => new MongoInsertEntityQuery(this, t);
        [DocgenIgnore]
        public MongoUpdateEntityQuery GetUpdateEntityQuery(Type t) => new MongoUpdateEntityQuery(this, t);
        [DocgenIgnore]
        public MongoUpdateMultiEntityQuery GetUpdateMultiEntityQuery(Type t) => new MongoUpdateMultiEntityQuery(this, t);
        [DocgenIgnore]
        public MongoDeleteEntityQuery GetDeleteEntityQuery(Type t) => new MongoDeleteEntityQuery(this, t);
        [DocgenIgnore]
        public MongoDeleteMultiEntityQuery GetDeleteMultiEntityQuery(Type t) => new MongoDeleteMultiEntityQuery(this, t);
        [DocgenIgnore]
        public MongoSelectQuery GetSelectQuery(Type t, bool expandReference = false) => new MongoSelectQuery(this, t, expandReference);
        [DocgenIgnore]
        public MongoCountQuery GetCountQuery(Type t) => new MongoCountQuery(this, t);

        private TagCollection mTags = null;

        /// <summary>
        /// Gets or sets tags of the connection.
        /// </summary>
        public TagCollection Tags => mTags ?? (mTags = new TagCollection());
    }
}

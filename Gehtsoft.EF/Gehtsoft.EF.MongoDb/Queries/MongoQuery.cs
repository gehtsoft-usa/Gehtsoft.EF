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
    public abstract class MongoQuery : IDisposable
    {
        protected Type Type { get; }
        protected BsonEntityDescription Description { get; }

        protected string CollectionName => Description.Table ?? Description.EntityType.Name;

        private IMongoCollection<BsonDocument> mCollection = null;

        public IMongoCollection<BsonDocument> Collection => mCollection ?? (mCollection = Connection.Database.GetCollection<BsonDocument>(CollectionName));

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
            Description = AllEntities.Inst.FindType(entityType);
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

        public abstract Task ExecuteAsync();

        public abstract Task ExecuteAsync(CancellationToken token);

        public abstract Task ExecuteAsync(object entity);

        public abstract Task ExecuteAsync(object entity, CancellationToken token);

        public void Execute()
        {
            var t = ExecuteAsync();
            t.Wait();
        }

        public void Execute(object entity)
        {
            var t = ExecuteAsync(entity);
            t.Wait();
        }

        private static readonly Dictionary<Type, Dictionary<string, string>> gPathCache = new Dictionary<Type, Dictionary<string, string>>();

        private class CurrentElementOfPath
        {
            internal bool IsArray { get; set; }
            internal Type ValueType { get; set; }
            internal BsonEntityDescription Entity { get; set; }
        }

        internal string TranslatePath(string path)
        {
            if (!gPathCache.TryGetValue(Type, out Dictionary<string, string> cache))
            {
                cache = new Dictionary<string, string>();
                gPathCache[Type] = cache;
            }

            if (cache.TryGetValue(path, out string translatedPath))
                return translatedPath;

            string[] elements = path.Split('.');

            CurrentElementOfPath currInfo = new CurrentElementOfPath()
            {
                IsArray = false,
                ValueType = Type,
                Entity = Description,
            };

            StringBuilder builder = new StringBuilder();

            foreach (string element in elements)
            {
                if (builder.Length > 0)
                    builder.Append('.');

                bool isInteger = element.Length > 0;

                foreach (char c in element)
                {
                    if (!char.IsDigit(c))
                    {
                        isInteger = false;
                        break;
                    }
                }

                if (isInteger)
                {
                    if (!currInfo.IsArray)
                        throw new EfMongoDbException(EfMongoDbExceptionCode.PropertyNotFound);
                    builder.Append(element);
                    currInfo = new CurrentElementOfPath()
                    {
                        IsArray = false,
                        Entity = currInfo.Entity,
                        ValueType = currInfo.ValueType,
                    };
                    continue;
                }

                if (currInfo.Entity == null)
                    throw new EfMongoDbException(EfMongoDbExceptionCode.PropertyNotFound);

                if (!currInfo.Entity.FieldsIndex.TryGetValue(element, out BsonEntityField field))
                    throw new EfMongoDbException(EfMongoDbExceptionCode.PropertyNotFound);

                builder.Append(field.FieldName ?? field.PropertyAccessor.Name);

                currInfo = new CurrentElementOfPath()
                {
                    IsArray = field.IsArray,
                    ValueType = field.PropertyElementType,
                    Entity = field.ReferencedEntity,
                };
            }

            translatedPath = builder.ToString();
            cache[path] = translatedPath;
            return translatedPath;
        }
    }

    public static class MongoQueryExtension
    {
        public static void Execute<T>(this MongoQuery query, T entity) => query.Execute(entity);
        public static void Execute<T>(this MongoQuery query, IEnumerable<T> entities) => query.Execute(entities);
    }
}

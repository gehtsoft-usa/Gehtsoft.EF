using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gehtsoft.EF.Bson;

namespace Gehtsoft.EF.MongoDb
{
    internal class PathTranslator : IMongoPathResolver
    {
        private BsonEntityDescription Description { get; }

        private readonly Type EntityType;

        public PathTranslator(Type entityType, BsonEntityDescription description)
        {
            EntityType = entityType;
            Description = description;
        }

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, string>> gPathCache = new ConcurrentDictionary<Type, ConcurrentDictionary<string, string>>();

        private class CurrentElementOfPath
        {
            internal bool IsArray { get; set; }
            internal Type ValueType { get; set; }
            internal BsonEntityDescription Entity { get; set; }
        }

        private static ConcurrentDictionary<string, string> GetCache(Type entityType)
        {
            if (!gPathCache.TryGetValue(entityType, out var cache))
            {
                cache = new ConcurrentDictionary<string, string>();
                gPathCache.TryAdd(entityType, cache);
            }
            return cache;
        }

        private static string GetPath(Type entityType, string path)
        {
            var dict = GetCache(entityType);
            if (dict.TryGetValue(path, out var s))
                return s;
            return null;
        }

        private static void SetPath(Type entityType, string path, string value)
        {
            var dict = GetCache(entityType);
            dict.TryAdd(path, value);
        }

        public string TranslatePath(string path)
        {
            var s = GetPath(EntityType, path);
            if (s != null)
                return s;

            string[] elements = path.Split('.');

            CurrentElementOfPath currInfo = new CurrentElementOfPath()
            {
                IsArray = false,
                ValueType = EntityType,
                Entity = Description,
            };

            StringBuilder builder = new StringBuilder();

            foreach (string element in elements)
            {
                if (builder.Length > 0)
                    builder.Append('.');

                bool isInteger = element.Length > 0 && element.All(c => char.IsDigit(c));

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
                }
                else
                {
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
            }

            s = builder.ToString();
            SetPath(EntityType, path, s);
            return s;
        }
    }
}

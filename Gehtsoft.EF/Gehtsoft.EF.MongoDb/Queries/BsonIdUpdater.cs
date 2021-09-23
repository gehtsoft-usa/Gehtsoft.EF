using MongoDB.Bson;
using Gehtsoft.EF.Bson;

namespace Gehtsoft.EF.MongoDb
{
    internal static class BsonIdUpdater
    {
        public static void UpdateId(this object entity, BsonEntityDescription description)
        {
            if (description.PrimaryKey != null &&
                description.PrimaryKey.IsAutoId &&
                description.PrimaryKey.PropertyElementType == typeof(ObjectId) &&
                (description.PrimaryKey.PropertyAccessor.GetValue(entity) == null || ((ObjectId)description.PrimaryKey.PropertyAccessor.GetValue(entity)) == ObjectId.Empty))
                description.PrimaryKey.PropertyAccessor.SetValue(entity, ObjectId.GenerateNewId());
        }
    }
}

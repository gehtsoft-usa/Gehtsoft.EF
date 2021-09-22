using MongoDB.Bson;
using Xunit.Sdk;

namespace Gehtsoft.EF.Test.Utils
{
    public static class BsonAssertionsExtensions
    {
        public static BsonDocumentAssertions Should(this BsonDocument document) => new BsonDocumentAssertions(document);
        public static BsonValueAssertions Should(this BsonValue document) => new BsonValueAssertions(document);
        public static BsonValueAssertions Should(this BsonArray document) => new BsonValueAssertions(document);

        public static object ValueOf(this BsonValue value)
        {
            object subject;

            if (value.IsBsonNull)
                subject = null;
            else if (value.IsInt32)
                subject = value.AsInt32;
            else if (value.IsInt64)
                subject = value.AsInt64;
            else if (value.IsDouble)
                subject = value.AsDouble;
            else if (value.IsDecimal128)
                subject = (decimal)value.AsDecimal128;
            else if (value.IsValidDateTime)
                subject = value.ToUniversalTime();
            else if (value.IsString)
                subject = value.AsString;
            else if (value.IsBsonBinaryData)
                subject = value.AsBsonBinaryData;
            else if (value.IsObjectId)
                subject = value.AsObjectId;
            else if (value.IsBoolean)
                subject = value.AsBoolean;
            else
                throw new XunitException("Only int, double, decimal, boolean, datetime, objectid, string, byte[] types are supported");

            return subject;
        }
    }
}

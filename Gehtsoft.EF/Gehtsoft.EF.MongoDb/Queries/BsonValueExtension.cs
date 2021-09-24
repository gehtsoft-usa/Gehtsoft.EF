using System;
using Gehtsoft.EF.Bson;
using MongoDB.Bson;
using System.Reflection;

namespace Gehtsoft.EF.MongoDb
{
    internal static class BsonValueExtension
    {
        public static object ConvertTo(this BsonValue value, Type type)
        {
            if (value == null || value.IsBsonNull)
            {
                if (type.IsValueType)
                    return Activator.CreateInstance(type);
                else
                    return null;
            }

            if (type == typeof(object))
                return BsonTypeMapper.MapToDotNetValue(value);
            if (type == typeof(string))
                return value.AsString;
            if (type == typeof(int))
                return value.AsInt32;
            if (type == typeof(int?))
                return (int?)ConvertTo(value, typeof(int));
            if (type == typeof(long))
                return value.AsInt64;
            if (type == typeof(long?))
                return (long?)ConvertTo(value, typeof(long));
            if (type == typeof(double))
                return value.AsDouble;
            if (type == typeof(double?))
                return (double?)ConvertTo(value, typeof(double));
            if (type == typeof(DateTime))
            {
                var v = value.AsBsonDateTime.ToUniversalTime();
                if (EntityToBsonController.ReturnDateTimeAsLocalByDefault)
                    v = v.ToLocalTime();
                return v;
            }

            if (type == typeof(DateTime?))
                return (DateTime?)ConvertTo(value, typeof(DateTime));
            if (type == typeof(bool))
                return value.AsBoolean;
            if (type == typeof(bool?))
                return (bool?)ConvertTo(value, typeof(bool));
            if (type == typeof(decimal))
                return value.AsDecimal;
            if (type == typeof(decimal?))
                return (decimal?)ConvertTo(value, typeof(decimal));
            if (type == typeof(byte[]))
                return value.AsBsonBinaryData.Bytes;
            if (type == typeof(ObjectId))
                return value.AsObjectId;
            if (type == typeof(ObjectId?))
                return (ObjectId?)ConvertTo(value, typeof(ObjectId));
            if (type == typeof(Guid))
            {
                string s = value.AsString;
                if (string.IsNullOrEmpty(s))
                    return Guid.Empty;
                if (!Guid.TryParse(s, out Guid g))
                    return Guid.Empty;
                return g;
            }

            if (type == typeof(Guid?))
                return (Guid?)ConvertTo(value, typeof(Guid));

            if (type.GetTypeInfo().IsArray && value.IsBsonArray)
            {
                Type elementType = type.GetTypeInfo().GetElementType();
                BsonArray arrSrc = value.AsBsonArray;
                int length = arrSrc.Count;
                object arrRes = Activator.CreateInstance(type, new object[] { length });
                Array arrDst = (Array)arrRes;
                for (int i = 0; i < length; i++)
                    arrDst.SetValue(ConvertTo(arrSrc[i], elementType), i);
                return arrRes;
            }

            if (value.IsBsonDocument)
            {
                if (type == typeof(BsonDocument))
                    return value.AsBsonDocument;

                return value.AsBsonDocument.ToEntity(type);
            }
            throw new Gehtsoft.EF.Bson.BsonException(BsonExceptionCode.TypeIsNotSupported);
        }
    }
}

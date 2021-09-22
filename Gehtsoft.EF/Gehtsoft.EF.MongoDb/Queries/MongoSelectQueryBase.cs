using System;
using System.Collections.Generic;
using System.Linq;
using Gehtsoft.EF.Bson;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Reflection;

namespace Gehtsoft.EF.MongoDb
{
    public abstract class MongoSelectQueryBase : MongoQueryWithCondition
    {
        private List<BsonDocument> mResultSet = null;
        private BsonDocument mCurrentRow = null;
        protected int mCurrentRowIdx;

        public List<BsonDocument> ResultSet
        {
            get => mResultSet;
            set
            {
                mResultSet = value;
                mCurrentRow = null;
                mCurrentRowIdx = -1;
            }
        }

        protected MongoSelectQueryBase(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public bool ReadNext()
        {
            if (mResultSet == null || mCurrentRowIdx >= mResultSet.Count - 1)
                return false;
            mCurrentRowIdx++;
            mCurrentRow = mResultSet[mCurrentRowIdx];
            return true;
        }

        public T ReadOne<T>() where T : class
        {
            if (!ReadNext())
                return null;

            return GetEntity<T>();
        }

        public object GetEntity(Type type)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            if (type != EntityType)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
            return mCurrentRow.ToEntity(Type);
        }

        public T GetEntity<T>() where T : class => GetEntity(typeof(T)) as T;

        public BsonDocument GetDocument() => mCurrentRow;

        protected object ConvertValue(BsonValue value, Type type)
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
                return value.IsBsonNull ? (string)null : value.AsString;
            if (type == typeof(int))
                return value.AsInt32;
            if (type == typeof(int?))
                return value.IsBsonNull ? null : (int?)ConvertValue(value, typeof(int));
            if (type == typeof(long))
                return value.AsInt64;
            if (type == typeof(long?))
                return value.IsBsonNull ? null : (long?)ConvertValue(value, typeof(long));
            if (type == typeof(double))
                return value.AsDouble;
            if (type == typeof(double?))
                return value.IsBsonNull ? null : (double?)ConvertValue(value, typeof(double));
            if (type == typeof(DateTime))
            {
                if (EntityToBsonController.ReturnDateTimeAsLocalByDefault)
                    return value.AsBsonDateTime.ToUniversalTime().ToLocalTime();
                else
                    value.AsBsonDateTime.ToUniversalTime();
            }

            if (type == typeof(DateTime?))
                return value.IsBsonNull ? null : (DateTime?)ConvertValue(value, typeof(DateTime));
            if (type == typeof(bool))
                return new DateTime(value.AsBsonDateTime.ToUniversalTime().Ticks, DateTimeKind.Unspecified);
            if (type == typeof(bool?))
                return value.IsBsonNull ? null : (bool?)ConvertValue(value, typeof(bool));
            if (type == typeof(decimal))
                return value.AsDecimal;
            if (type == typeof(decimal?))
                return value.IsBsonNull ? null : (decimal?)ConvertValue(value, typeof(decimal));
            if (type == typeof(byte[]))
                return value.IsBsonNull ? (byte[])null : value.AsBsonBinaryData.Bytes;
            if (type == typeof(ObjectId))
                return value.AsObjectId;
            if (type == typeof(ObjectId?))
                return value.IsBsonNull ? null : (ObjectId?)ConvertValue(value, typeof(ObjectId));
            if (type == typeof(Guid))
            {
                string s = value.AsString;
                if (s == null)
                    return Guid.Empty;
                if (!Guid.TryParse(s, out Guid g))
                    return Guid.Empty;
                return g;
            }
            if (type == typeof(Guid?))
                return value.IsBsonNull ? null : (Guid?)ConvertValue(value, typeof(Guid));

            if (type.GetTypeInfo().IsArray && value.IsBsonArray)
            {
                if (value.IsBsonNull)
                    return null;

                Type elementType = type.GetTypeInfo().GetElementType();
                BsonArray arrSrc = value.AsBsonArray;
                int length = arrSrc.Count;
                object arrRes = Activator.CreateInstance(type, new object[] { length });
                Array arrDst = (Array)arrRes;
                for (int i = 0; i < length; i++)
                    arrDst.SetValue(ConvertValue(arrSrc[i], elementType), i);
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

        public object GetValue(int column, Type type)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return ConvertValue(mCurrentRow[column], type);
        }

        public object GetValue(string column, Type type)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);

            return ConvertValue(mCurrentRow.GetValue(column, BsonNull.Value), type);
        }

        public T GetValue<T>(int column) => (T)GetValue(column, typeof(T));

        public T GetValue<T>(string field) => (T)GetValue(field, typeof(T));

        public bool IsNull(int column)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return mCurrentRow[column].IsBsonNull;
        }

        public bool IsNull(string field)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return mCurrentRow.GetValue(field, BsonNull.Value).IsBsonNull;
        }

        public int FieldCount => mCurrentRow?.Values.Count<BsonValue>() ?? 0;

        public string FieldName(int column) => mCurrentRow.GetElement(column).Name;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Entities;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Reflection;
using System.Threading;

namespace Gehtsoft.EF.MongoDb
{
    public class MongoCountQuery : MongoQueryWithCondition
    {
        private long? mRowCount = null;

        public long RowCount
        {
            get
            {
                if (mRowCount == null)
                    Execute();
                return mRowCount ?? 0;
            }
        }

        internal MongoCountQuery(MongoConnection connection, Type entityType) : base(connection, entityType)
        {
        }

        public override async Task ExecuteAsync()
        {
            mRowCount = await Collection.CountDocumentsAsync(FilterBuilder.ToBsonDocument());
        }
        public override async Task ExecuteAsync(CancellationToken token)
        {
            mRowCount = await Collection.CountDocumentsAsync(FilterBuilder.ToBsonDocument(), null, token);
        }

        public override Task ExecuteAsync(object entity)
        {
            throw new InvalidOperationException();
        }
        public override Task ExecuteAsync(object entity, CancellationToken token)
        {
            throw new InvalidOperationException();
        }
    }

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

    public class MongoSelectQuery : MongoSelectQueryBase
    {
        private readonly bool mExpandExternal = false;
        private List<Tuple<string, bool>> mResultSet = null;

        internal MongoSelectQuery(MongoConnection connection, Type entityType, bool expandExternalReferences) : base(connection, entityType)
        {
            mExpandExternal = expandExternalReferences;
        }

        public int Skip { get; set; }
        public int Limit { get; set; }

        private SortDefinition<BsonDocument> mSort = null;

        public void AddToResultset(string path)
        {
            if (mResultSet == null)
                mResultSet = new List<Tuple<string, bool>>();
            mResultSet.Add(new Tuple<string, bool>(TranslatePath(path), true));
        }

        public void ExcludeFromResultset(string path)
        {
            if (mResultSet == null)
                mResultSet = new List<Tuple<string, bool>>();
            mResultSet.Add(new Tuple<string, bool>(TranslatePath(path), false));
        }

        public void AddOrderBy(string property, SortDir direction)
        {
            FieldDefinition<BsonDocument> field = TranslatePath(property);

            if (mSort == null)
            {
                SortDefinitionBuilder<BsonDocument> builder = new SortDefinitionBuilder<BsonDocument>();

                if (direction == SortDir.Asc)
                    mSort = builder.Ascending(field);
                else
                    mSort = builder.Descending(field);
            }
            else
            {
                if (direction == SortDir.Asc)
                    mSort = mSort.Ascending(field);
                else
                    mSort = mSort.Descending(field);
            }
        }

        private async Task ExecuteAsyncCore(CancellationToken token)
        {
            ResultSet = null;

            FilterDefinition<BsonDocument> filter = FilterBuilder.ToBsonDocument();

            if (mExpandExternal)
            {
                throw new NotImplementedException();
            }
            else
            {
                FindOptions<BsonDocument> options = new FindOptions<BsonDocument>();

                if (Skip > 0 || Limit > 0)
                {
                    options.Limit = Limit;
                    options.Skip = Skip;
                }

                if (mSort != null)
                    options.Sort = mSort;

                if (mResultSet != null)
                {
                    ProjectionDefinition<BsonDocument> projection = null;

                    foreach (Tuple<string, bool> v in mResultSet)
                    {
                        if (projection == null)
                            projection = v.Item2 ? Builders<BsonDocument>.Projection.Include(v.Item1) : Builders<BsonDocument>.Projection.Exclude(v.Item1);
                        else
                            projection = v.Item2 ? projection.Include(v.Item1) : projection.Exclude(v.Item1);
                    }
                    options.Projection = projection;
                }

                using (IAsyncCursor<BsonDocument> cursor = await Collection.FindAsync(filter, options, token))
                {
                    List<BsonDocument> rs = new List<BsonDocument>();
                    while (await cursor.MoveNextAsync(token))
                    {
                        IEnumerable<BsonDocument> batch = cursor.Current;
                        rs.AddRange(batch);
                    }
                    ResultSet = rs;
                }
            }
        }

        public override Task ExecuteAsync(object entity)
        {
            throw new InvalidOperationException();
        }
        public override Task ExecuteAsync(object entity, CancellationToken token)
        {
            throw new InvalidOperationException();
        }

        public EntityCollection<T> ReadAll<T>() where T : class => ReadAll<EntityCollection<T>, T>();

        public TC ReadAll<TC, T>() where TC : EntityCollection<T>, new() where T : class
        {
            if (ResultSet == null)
                Execute();

            TC coll = new TC();
            while (ReadNext())
                coll.Add(GetEntity<T>());
            return coll;
        }

        public override Task ExecuteAsync() => ExecuteAsyncCore(default);

        public override Task ExecuteAsync(CancellationToken token) => ExecuteAsyncCore(token);
    }
}

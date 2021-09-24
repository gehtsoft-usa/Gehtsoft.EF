using System;
using System.Collections.Generic;
using System.Linq;
using Gehtsoft.EF.Bson;
using Gehtsoft.EF.Utils;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Gehtsoft.EF.MongoDb
{
    /// <summary>
    /// The base class for select queries.
    ///
    /// This class is an abstract class. Use <see cref="MongoSelectQuery"/> instead. This class is introduced
    /// to support joined projections in future releases of the framework.
    /// </summary>
    public abstract class MongoSelectQueryBase : MongoQueryWithCondition
    {
        private List<BsonDocument> mResultSet = null;
        private BsonDocument mCurrentRow = null;
        protected int mCurrentRowIdx;

        [DocgenIgnore]
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

        /// <summary>
        /// Reads the next row of the cursor.
        ///
        /// The method returns `true` if a row is succesfully read and `false` if the end of the cursor is reached.
        /// </summary>
        /// <returns></returns>
        public bool ReadNext()
        {
            if (mResultSet == null || mCurrentRowIdx >= mResultSet.Count - 1)
                return false;
            mCurrentRowIdx++;
            mCurrentRow = mResultSet[mCurrentRowIdx];
            return true;
        }

        /// <summary>
        /// Reads the next row as a entity of the specified type.
        ///
        /// The method returns `null` if the end of the cursor is reached.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T ReadOne<T>() where T : class
        {
            if (!ReadNext())
                return null;

            return GetEntity<T>();
        }

        /// <summary>
        /// Gets the current row as an entity of the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetEntity(Type type)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            if (type != EntityType)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NotAnEntity);
            return mCurrentRow.ToEntity(Type);
        }

        /// <summary>
        /// Gets the current row as an entity of the specified type (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetEntity<T>() where T : class => GetEntity(typeof(T)) as T;

        /// <summary>
        /// Gets the current row as BSON document.
        /// </summary>
        /// <returns></returns>
        public BsonDocument GetDocument() => mCurrentRow;

        protected object ConvertValue(BsonValue value, Type type) => value.ConvertTo(type);

        /// <summary>
        /// Gets a property of the current row by its index.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetValue(int column, Type type)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return ConvertValue(mCurrentRow[column], type);
        }

        /// <summary>
        /// Gets a property of the current row by the property name.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetValue(string column, Type type)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return ConvertValue(mCurrentRow.GetValue(column, BsonNull.Value), type);
        }

        /// <summary>
        /// Gets a property of the current row by its index (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        public T GetValue<T>(int column) => (T)GetValue(column, typeof(T));

        /// <summary>
        /// Gets a property of the current row by the property name (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <returns></returns>
        public T GetValue<T>(string field) => (T)GetValue(field, typeof(T));

        /// <summary>
        /// Checks the property for be a null value by its index.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public bool IsNull(int column)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return mCurrentRow[column].IsBsonNull;
        }

        /// <summary>
        /// Checks the property for be a null value by the property name.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public bool IsNull(string field)
        {
            if (mCurrentRow == null)
                throw new EfMongoDbException(EfMongoDbExceptionCode.NoRow);
            return mCurrentRow.GetValue(field, BsonNull.Value).IsBsonNull;
        }

        /// <summary>
        /// Returns the number of properties in the current row.
        ///
        /// Note: Unlike SQL databases you must read the row to check the number of columns/properties in a row.
        /// </summary>
        public int FieldCount => mCurrentRow?.Values.Count<BsonValue>() ?? 0;

        /// <summary>
        /// Returns the name of the property by its index.
        ///
        /// Note: Unlike SQL databases you must read the row to check the names of columns/properties in a row.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public string FieldName(int column) => mCurrentRow.GetElement(column).Name;
    }
}

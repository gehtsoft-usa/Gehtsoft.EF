using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    /// <summary>
    /// A collection of Sql Errors
    /// </summary>
    [Serializable]
    public class SqlErrorCollection : IReadOnlyList<SqlError>, ISerializable
    {
        private readonly List<SqlError> mList = new List<SqlError>();

        internal SqlErrorCollection()
        {
        }

        /// <summary>
        /// Returns the error by its index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SqlError this[int index] => ((IReadOnlyList<SqlError>)mList)[index];

        /// <summary>
        /// Returns the number of errors
        /// </summary>
        public int Count => ((IReadOnlyCollection<SqlError>)mList).Count;

        public IEnumerator<SqlError> GetEnumerator()
        {
            return ((IEnumerable<SqlError>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlError error) => mList.Add(error);

        internal void Add(string source, int line, int position, string error) => Add(new SqlError(source, line, position, error));

        internal static SqlErrorCollection ToSqlErrors(string source, ParseResult r)
        {
            if (r == null)
                throw new ArgumentNullException(nameof(r));
            if (r.IsSuccess)
                throw new ArgumentException("Parser result is succesful", nameof(r));

            SqlErrorCollection collection = new SqlErrorCollection();
            foreach (var e in r.Errors)
                collection.Add(source, e.Position.Line, e.Position.Column, e.Message);
            return collection;
        }

        protected SqlErrorCollection(SerializationInfo info, StreamingContext context)
        {
            int count = (int)info.GetValue("count", typeof(int));
            for (int i = 0; i < count; i++)
                mList.Add((SqlError)info.GetValue($"item{i}", typeof(SqlError)));
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("count", Count);
            for (int i = 0; i < Count; i++)
                info.AddValue($"item{i}", this[i]);
        }
    }
}

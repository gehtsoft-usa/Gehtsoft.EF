using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// The accessor to the entities via LINQ queries.
    ///
    /// Use <see cref="QueryableEntityProvider"/> to get an instance of the object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryableEntity<T> : IOrderedQueryable<T>
    {
        private readonly QueryableEntityProvider mProvider;

        internal QueryableEntity(QueryableEntityProvider provider)
        {
            mProvider = provider;
            Expression = Expression.Constant(this);
        }

        internal QueryableEntity(QueryableEntityProvider provider, Expression expression)
        {
            mProvider = provider;
            Expression = expression;
        }

        [DocgenIgnore]
        public IEnumerator<T> GetEnumerator()
        {
            return mProvider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
        }

        [DocgenIgnore]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [DocgenIgnore]
        public Expression Expression { get; }

        [DocgenIgnore]
        public Type ElementType => typeof(T);

        [DocgenIgnore]
        public IQueryProvider Provider => mProvider;

        /// <summary>
        /// Inserts the entity.
        /// </summary>
        /// <param name="value"></param>
        public void Insert(T value) => mProvider.Insert<T>(value);

        /// <summary>
        /// Updates the entity.
        /// </summary>
        /// <param name="value"></param>
        public void Update(T value) => mProvider.Update<T>(value);

        /// <summary>
        /// Deletes the entity.
        /// </summary>
        /// <param name="value"></param>
        public void Delete(T value) => mProvider.Delete<T>(value);
    }
}

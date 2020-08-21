using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    public class QueryableEntity<T> : IOrderedQueryable<T>
    {
        private readonly QueryableEntityProvider mProvider;

        public QueryableEntity(QueryableEntityProvider provider)
        {
            mProvider = provider;
            Expression = Expression.Constant(this);
        }

        public QueryableEntity(QueryableEntityProvider provider, Expression expression)
        {
            mProvider = provider;
            Expression = expression;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return mProvider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Expression Expression { get; private set; }
        
        public Type ElementType => typeof(T);
        
        public IQueryProvider Provider => mProvider;

        public void Insert(T value) => mProvider.Insert<T>(value);

        public void Update(T value) => mProvider.Update<T>(value);

        public void Delete(T value) => mProvider.Delete<T>(value);
    }
}

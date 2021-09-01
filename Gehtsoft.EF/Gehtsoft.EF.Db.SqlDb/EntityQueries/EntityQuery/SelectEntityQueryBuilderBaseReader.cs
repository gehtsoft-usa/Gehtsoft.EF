using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The query result reader.
    ///
    /// Use this class to bind <see cref="SelectEntitiesQueryBase"/> results into
    /// custom object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SelectEntityQueryReader<T> : SelectQueryResultBinder
        where T : class
    {
        private readonly SelectEntitiesQueryBase mQuery;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="query"></param>
        public SelectEntityQueryReader(SelectEntitiesQueryBase query) : base(typeof(T))
        {
            mQuery = query;
        }

        /// <summary>
        /// Reads all rows and applies the function until function returns `false`.
        /// </summary>
        /// <param name="action"></param>
        public void Scan(Func<T, bool> action)
        {
            while (true)
            {
                T t = ReadOne();
                if (t == null)
                    return;
                if (!action(t))
                    return;
            }
        }

        /// <summary>
        /// Reads all raws and applies the action.
        /// </summary>
        /// <param name="action"></param>
        public void ScanAll(Action<T> action)
            => Scan(t =>
                {
                    action(t);
                    return true;
                });

        /// <summary>
        /// Reads one object.
        /// </summary>
        /// <returns></returns>
        public T ReadOne()
        {
            if (Rules.Count == 0)
                AutoBindType();

            if (!mQuery.ReadNext())
                return null;

            T t = Activator.CreateInstance<T>();

            Read(mQuery, t);

            return t;
        }

        /// <summary>
        /// Reads all objects.
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <returns></returns>
        public TC ReadAll<TC>()
            where TC : IList<T>, new()
        {
            TC rc = new TC();
            T t;
            while (true)
            {
                t = ReadOne();
                if (t == null)
                    break;
                rc.Add(t);
            }
            return rc;
        }

        /// <summary>
        /// Reads all objects into entity collection.
        /// </summary>
        /// <returns></returns>
        public EntityCollection<T> ReadAll() => ReadAll<EntityCollection<T>>();
    }
}
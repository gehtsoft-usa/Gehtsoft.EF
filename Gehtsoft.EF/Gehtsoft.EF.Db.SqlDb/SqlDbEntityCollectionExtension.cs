using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The SQL related extensions to the entity collection.
    /// </summary>
    public static class SqlDbEntityCollectionExtension
    {
        /// <summary>
        /// Finds the entity by column value (column defined by the table column definition).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="column"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static int Find<T>(this IEntityAccessor<T> collection, TableDescriptor.ColumnInfo column, object value)
        {
            for (int i = 0; i < collection.Count; i++)
                if (Equals(column.PropertyAccessor.GetValue(collection[i]), value))
                    return i;
            return -1;
        }

        /// <summary>
        /// Finds the entity by column value (column defined by name).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="column"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int Find<T>(this IEntityAccessor<T> collection, string column, object value)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];
            return Find(collection, descriptor[column], value);
        }

        /// <summary>
        /// Finds the entity by primary key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int FindByPK<T>(this IEntityAccessor<T> collection, object value)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];
            return Find(collection, descriptor.PrimaryKey, value);
        }
    }
}

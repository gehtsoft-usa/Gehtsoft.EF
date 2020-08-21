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
    public static class SqlDbEntityCollectionExtension
    {
        private static int Find<T>(this EntityCollection<T> collection, TableDescriptor.ColumnInfo column, object value)
        {
            for (int i = 0; i < collection.Count; i++)
                if (Equals(column.PropertyAccessor.GetValue(collection[i]), value))
                    return i;
            return -1;
        }

        private static T Get<T>(this EntityCollection<T> collection, TableDescriptor.ColumnInfo column, object value)
        {
            int index = Find(collection, column, value);
            if (index < 0)
                return default(T);
            return collection[index];
        }

        public static int Find<T>(this EntityCollection<T> collection, string column, object value)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];
            return Find(collection, descriptor[column], value);
        }

        public static int Get<T>(this EntityCollection<T> collection, string column, object value)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];
            return Find(collection, descriptor[column], value);
        }

        public static int FindByPK<T>(this EntityCollection<T> collection, object value)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];
            return Find(collection, descriptor.PrimaryKey, value);
        }

        public static int GetByPK<T>(this EntityCollection<T> collection, object value)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];
            return Find(collection, descriptor.PrimaryKey, value);
        }
    }
}

using System;
using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class EntityDescriptor
    {
        public bool Obsolete { get; internal set; }
        public Type EntityType { get; internal set; }
        public TableDescriptor TableDescriptor { get; internal set; }
        public TableDescriptor.ColumnInfo SelfReference { get; internal set; }
        public TableDescriptor.ColumnInfo this[string id] => TableDescriptor[id];
        public TableDescriptor.ColumnInfo PrimaryKey => TableDescriptor.PrimaryKey;
        private Dictionary<Type, object> mTags = null;

        public object this[Type type]
        {
            get
            {
                if (mTags == null)
                    return null;
                if (mTags.TryGetValue(type, out object tag))
                    return tag;
                return null;
            }
            set
            {
                if (mTags == null)
                    mTags = new Dictionary<Type, object>();
                mTags[type] = value;
            }
        }

        public void SetTag<T>(T tag) where T : class => this[typeof(T)] = tag;
        public T GetTag<T>() where T : class => this[typeof(T)] as T;
    }
}

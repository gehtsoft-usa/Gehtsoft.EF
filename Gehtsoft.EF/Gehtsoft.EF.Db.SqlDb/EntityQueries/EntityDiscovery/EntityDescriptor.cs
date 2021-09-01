using System;
using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The descriptor of the entity.
    /// </summary>
    public class EntityDescriptor
    {
        /// <summary>
        /// The flag indicating that the entity is obsolete.
        /// </summary>
        public bool Obsolete { get; internal set; }
        /// <summary>
        /// The run-time type associated with the entity.
        /// </summary>
        public Type EntityType { get; internal set; }
        /// <summary>
        /// The SQL table descriptor.
        /// </summary>
        public TableDescriptor TableDescriptor { get; internal set; }
        /// <summary>
        /// The column which is used to self-reference for tree/hierarchical tables.
        /// </summary>
        public TableDescriptor.ColumnInfo SelfReference { get; internal set; }
        /// <summary>
        /// Gets the column by its identifier.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public TableDescriptor.ColumnInfo this[string id] => TableDescriptor[id];
        /// <summary>
        /// Gets the primary key of the table.
        /// </summary>
        public TableDescriptor.ColumnInfo PrimaryKey => TableDescriptor.PrimaryKey;

        private Dictionary<Type, object> mTags = null;

        /// <summary>
        /// Gets or sets a tag associated with the entity.
        /// 
        /// Tag is any user-specific information the application may associated with the entity.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Sets the tag.
        /// 
        /// Tag is any user-specific information the application may associated with the entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        public void SetTag<T>(T tag) where T : class => this[typeof(T)] = tag;
        /// <summary>
        /// Gets the tag.
        /// 
        /// Tag is any user-specific information the application may associated with the entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetTag<T>() where T : class => this[typeof(T)] as T;
    }
}

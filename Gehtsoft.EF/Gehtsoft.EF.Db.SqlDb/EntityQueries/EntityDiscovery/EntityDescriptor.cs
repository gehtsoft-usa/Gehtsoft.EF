using System;
using System.Collections.Generic;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

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

        /// <summary>
        /// The dynamic properties attribute of the entity, or `null` if the entity does not
        /// own a dynamic property set.
        ///
        /// The attribute carries the shaping options of the dynamic-property side table.
        /// </summary>
        public DynamicPropertiesAttribute DynamicProperties { get; internal set; }

        /// <summary>
        /// The flag indicating whether the entity owns a dynamic property set.
        ///
        /// See also <see cref="DynamicPropertiesAttribute"/>.
        /// </summary>
        public bool HasDynamicProperties => DynamicProperties != null;

        private TableDescriptor mDynamicPropertiesTable;

        /// <summary>
        /// The descriptor of the EAV side table that stores the dynamic property set, or
        /// `null` if the entity does not own a dynamic property set.
        ///
        /// The descriptor is a regular table descriptor and can be used with the query
        /// builder as any other table. It is synthesized on first access and cached.
        /// </summary>
        public TableDescriptor DynamicPropertiesTable
        {
            get
            {
                if (!HasDynamicProperties)
                    return null;
                if (mDynamicPropertiesTable == null)
                    mDynamicPropertiesTable = DynamicPropertiesTableBuilder.Build(this);
                return mDynamicPropertiesTable;
            }
        }

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

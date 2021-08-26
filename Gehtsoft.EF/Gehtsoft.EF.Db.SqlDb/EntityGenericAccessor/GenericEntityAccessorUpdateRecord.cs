using System;
using System.Collections.Generic;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UpdateRecordPropertyAttribute : Attribute
    {
        /// <summary>
        /// The name of the entity property. By default - the same name as the filter property.
        /// </summary>
        public string PropertyName { get; set; }
    }

    /// <summary>
    /// <para>Generic update record class for the generic accessor.</para>
    /// <para>
    /// The whole idea of generic filter is that the developer derives operation-specific update class from this one
    /// and defines a set of properties making them up using UpdateRecordProperty attribute.
    /// </para>
    /// </summary>
    public class GenericEntityAccessorUpdateRecord
    {
        private readonly Type mEntityType;
        private readonly TypeInfo mUpdateRecordTypeInfo;

        internal class FieldInfo
        {
            internal string EntityPath { get; }
            internal PropertyInfo RecordProperty { get; }

            internal FieldInfo(PropertyInfo recordProperty, string entityPath)
            {
                RecordProperty = recordProperty;
                EntityPath = entityPath;
            }
        }

        private List<FieldInfo> mFields = null;

        internal IEnumerable<FieldInfo> Fields
        {
            get
            {
                if (mFields == null)
                    InitializeRecordData();
                return mFields;
            }
        }

        /// <summary>
        /// Add a field description for automatic binding
        /// </summary>
        /// <param name="filterPropertyName"></param>
        /// <param name="associatedPropertyName"></param>
        protected void AddUpdateField(string filterPropertyName, string associatedPropertyName)
        {
            if (filterPropertyName == null)
                throw new ArgumentException(nameof(filterPropertyName));

            PropertyInfo fpi = mUpdateRecordTypeInfo.GetProperty(filterPropertyName, BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public);
            if (fpi == null)
                throw new ArgumentException("Property is not found in the update record", nameof(filterPropertyName));
            EntityPathAccessor.PreparePath(mEntityType, associatedPropertyName);
            mFields.Add(new FieldInfo(fpi, associatedPropertyName));
        }

        /// <summary>
        /// Override to provide custom metadata for filter fields for automatic binding
        /// </summary>
        protected virtual void InitializeRecordData()
        {
            if (mCache.TryGetValue(this.GetType(), out List<FieldInfo> fields))
            {
                mFields = fields;
                return;
            }

            mFields = new List<FieldInfo>();
            PropertyInfo[] properties = mUpdateRecordTypeInfo.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                UpdateRecordPropertyAttribute attr = property.GetCustomAttribute<UpdateRecordPropertyAttribute>();
                if (attr != null)
                {
                    if (attr.PropertyName == null)
                        attr.PropertyName = property.Name;
                    AddUpdateField(property.Name, attr.PropertyName);
                }
            }

            mCache[this.GetType()] = mFields;
        }

        public virtual void Reset()
        {
            if (mFields == null)
                InitializeRecordData();

            foreach (FieldInfo fieldInfo in mFields)
                fieldInfo.RecordProperty.SetValue(this, null);
        }

        public void BindToQuery(MultiUpdateEntityQuery query) => BindToQueryImpl(query);

        /// <summary>
        /// Override to provide custom binding to the values
        /// </summary>
        /// <param name="query"></param>
        protected virtual void BindToQueryImpl(MultiUpdateEntityQuery query)
        {
            if (mFields == null)
                InitializeRecordData();

            foreach (FieldInfo fieldInfo in mFields)
            {
                object value = fieldInfo.RecordProperty.GetValue(this);
                query.AddUpdateColumn(fieldInfo.EntityPath, value);
            }
        }

        private static readonly Dictionary<Type, List<FieldInfo>> mCache = new Dictionary<Type, List<FieldInfo>>();

        public GenericEntityAccessorUpdateRecord(Type t)
        {
            var recordType = this.GetType();
            mUpdateRecordTypeInfo = recordType.GetTypeInfo();
            mEntityType = t;
            EntityAttribute attr = t.GetTypeInfo().GetCustomAttribute<EntityAttribute>();
            if (attr == null)
                throw new ArgumentException("Type is not an entity", nameof(t));
        }
    }

    public class GenericEntityAccessorUpdateRecordT<T> : GenericEntityAccessorUpdateRecord
    {
        public GenericEntityAccessorUpdateRecordT() : base(typeof(T))
        {
        }
    }
}
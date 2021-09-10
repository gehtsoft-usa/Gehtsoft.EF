using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor
{
    /// <summary>
    /// The attribute to mark a generic accessor filter property.
    /// 
    /// Apply it to a property of a class derived from <see cref="GenericEntityAccessorFilter"/>
    /// to bind the property automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class FilterPropertyAttribute : Attribute
    {
        /// <summary>
        /// The name of the entity property. By default - the same name as the filter property.
        /// </summary>
        public string PropertyName { get; set; }
        /// <summary>
        /// The filtering operation - by default - equality comparison
        /// </summary>
        public CmpOp Operation { get; set; } = CmpOp.Eq;
    }

    /// <summary>
    /// <para>Generic filter class for the generic accessor.</para>
    /// 
    /// <para>
    /// The whole idea of generic filter is that the developer just derives type-specific filter class from this one
    /// and just defines a set of properties making them up using FilterProperty attribute.
    /// </para>
    /// 
    /// <para>All filters are joined by AND</para>
    /// <para>
    /// 1) The filter properties types must be equal to entity property types in their nullable version, i.e. if the field is
    ///    int, the filter type shall be int?. the null value will mean that filter is not used.
    /// </para>
    /// <para>2) Should IsNull comparison be made, the filter property should be bool?. Null means that filter is inactive, true means IsNull and false means IsNotNull.</para>
    /// </summary>
    public class GenericEntityAccessorFilter
    {
        private readonly Type mEntityType;
        private readonly TypeInfo mFilterTypeInfo;

        internal class FieldInfo
        {
            internal string EntityPath { get; }
            internal PropertyInfo FilterProperty { get; }
            internal CmpOp CompareOperation { get; }

            internal FieldInfo(PropertyInfo filterProperty, string entityPath, CmpOp compareOperation)
            {
                FilterProperty = filterProperty;
                EntityPath = entityPath;
                CompareOperation = compareOperation;
            }
        }

        private List<FieldInfo> mFields = null;

        internal IEnumerable<FieldInfo> Fields
        {
            get
            {
                if (mFields == null)
                    InitializeFilterData();
                return mFields;
            }
        }

        /// <summary>
        /// Add a field description for automatic binding
        /// </summary>
        /// <param name="filterPropertyName"></param>
        /// <param name="associatedPropertyName"></param>
        /// <param name="operation"></param>
        protected void AddFilterField(string filterPropertyName, string associatedPropertyName, CmpOp operation)
        {
            if (filterPropertyName == null)
                throw new ArgumentException(nameof(filterPropertyName));

            PropertyInfo fpi = mFilterTypeInfo.GetProperty(filterPropertyName, BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public);
            if (fpi == null)
                throw new ArgumentException("Property is not found in the filter", nameof(filterPropertyName));

            EntityPathAccessor.PreparePath(mEntityType, associatedPropertyName);

            mFields.Add(new FieldInfo(fpi, associatedPropertyName, operation));
        }

        /// <summary>
        /// Override to provide custom metadata for filter fields for automatic binding
        /// </summary>
        protected virtual void InitializeFilterData()
        {
            if (mCache.TryGetValue(this.GetType(), out List<FieldInfo> fields))
            {
                mFields = fields;
                return;
            }

            mFields = new List<FieldInfo>();
            PropertyInfo[] properties = mFilterTypeInfo.GetProperties();
            foreach (PropertyInfo property in properties)
            {
                FilterPropertyAttribute attr = property.GetCustomAttribute<FilterPropertyAttribute>();
                if (attr != null)
                {
                    if (attr.PropertyName == null)
                        attr.PropertyName = property.Name;
                    AddFilterField(property.Name, attr.PropertyName, attr.Operation);
                }
            }

            mCache[this.GetType()] = mFields;
        }

        /// <summary>
        /// Resets all filter conditions.
        /// </summary>
        public virtual void Reset()
        {
            if (mFields == null)
                InitializeFilterData();

            foreach (FieldInfo fieldInfo in mFields)
                fieldInfo.FilterProperty.SetValue(this, null);
        }

        /// <summary>
        /// Binds the filter to a query.
        /// </summary>
        /// <param name="query"></param>
        public void BindToQuery(ConditionEntityQueryBase query) => BindToQueryImpl(query);

        /// <summary>
        /// Override to provide custom binding to the values
        /// </summary>
        /// <param name="query"></param>
        protected virtual void BindToQueryImpl(ConditionEntityQueryBase query)
        {
            if (mFields == null)
                InitializeFilterData();

            foreach (FieldInfo fieldInfo in mFields)
            {
                object value = fieldInfo.FilterProperty.GetValue(this);
                CmpOp op = fieldInfo.CompareOperation;

                if (value == null)
                    continue;

                //special handling
                if (op == CmpOp.IsNull && (value is bool?))
                {
                    bool v = (bool)(bool?)value;
                    query.Where.Property(fieldInfo.EntityPath).Is(v ? CmpOp.IsNull : CmpOp.NotNull);
                    continue;
                }

                if (op == CmpOp.NotNull && (value is bool?))
                {
                    bool v = (bool)(bool?)value;
                    query.Where.Property(fieldInfo.EntityPath).Is(v ? CmpOp.NotNull : CmpOp.IsNull);

                    continue;
                }

                if ((op == CmpOp.In || op == CmpOp.NotIn) && (value is ICollection || value is Array))
                {
                    query.Where.Property(fieldInfo.EntityPath).Is(op).Values(value);
                    continue;
                }

                query.Where.Add().Property(fieldInfo.EntityPath).Is(op).Value(value);
            }
        }

        private static readonly Dictionary<Type, List<FieldInfo>> mCache = new Dictionary<Type, List<FieldInfo>>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="t"></param>
        protected GenericEntityAccessorFilter(Type t)
        {
            var filterType = this.GetType();
            mFilterTypeInfo = filterType.GetTypeInfo();
            mEntityType = t;
            EntityAttribute attr = t.GetTypeInfo().GetCustomAttribute<EntityAttribute>();
            if (attr == null)
                throw new ArgumentException("Type is not an entity", nameof(t));
        }
    }

    public class GenericEntityAccessorFilterT<T> : GenericEntityAccessorFilter
    {
        public GenericEntityAccessorFilterT() : base(typeof(T))
        {
        }
    }
}
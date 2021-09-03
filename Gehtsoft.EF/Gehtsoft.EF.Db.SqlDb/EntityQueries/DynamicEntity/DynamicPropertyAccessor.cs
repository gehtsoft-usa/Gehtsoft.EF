using System;
using System.Runtime.CompilerServices;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;
using Microsoft.CSharp.RuntimeBinder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The implementation of the property accessor for a dynamic property
    /// </summary>
    [DocgenIgnore]
    public class DynamicPropertyAccessor : IPropertyAccessor
    {
        private readonly IDynamicEntityProperty mPropertyInfo;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="propertyInfo"></param>
        public DynamicPropertyAccessor(IDynamicEntityProperty propertyInfo)
        {
            mPropertyInfo = propertyInfo;
        }

        /// <summary>
        /// The property name
        /// </summary>
        public string Name => mPropertyInfo.Name;

        /// <summary>
        /// The property type.
        /// </summary>
        public Type PropertyType => mPropertyInfo.PropertyType;

        /// <summary>
        /// Gets the property value.
        /// </summary>
        /// <param name="thisObject"></param>
        /// <returns></returns>
        public object GetValue(object thisObject)
        {
            var binder = Binder.GetMember(CSharpBinderFlags.None, mPropertyInfo.Name, thisObject.GetType(),
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
            var callsite = CallSite<Func<CallSite, object, object>>.Create(binder);
            return callsite.Target(callsite, thisObject);
        }

        /// <summary>
        /// Sets the property value.
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="value"></param>
        public void SetValue(object thisObject, object value)
        {
            var binder = Binder.SetMember(CSharpBinderFlags.None, mPropertyInfo.Name, thisObject.GetType(),
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
            var callsite = CallSite<Func<CallSite, object, object, object>>.Create(binder);
            callsite.Target(callsite, thisObject, value);
        }

        /// <summary>
        /// Gets the custom attribute associated with the property.
        /// </summary>
        /// <param name="attributeType"></param>
        /// <returns></returns>
        public Attribute GetCustomAttribute(Type attributeType)
        {
            if (attributeType == typeof(EntityPropertyAttribute))
                return mPropertyInfo.EntityPropertyAttribute;
            if (attributeType == typeof(ObsoleteEntityPropertyAttribute))
                return mPropertyInfo.ObsoleteEntityPropertyAttribute;
            return null;
        }
    }
}
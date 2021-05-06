using System;
using System.Runtime.CompilerServices;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Microsoft.CSharp.RuntimeBinder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class DynamicPropertyAccessor : IPropertyAccessor
    {
        private readonly IDynamicEntityProperty mPropertyInfo;
        public DynamicPropertyAccessor(IDynamicEntityProperty propertyInfo)
        {
            mPropertyInfo = propertyInfo;
        }

        public string Name => mPropertyInfo.Name;
        public Type PropertyType => mPropertyInfo.PropertyType;

        public object GetValue(object thisObject)
        {
            var binder = Binder.GetMember(CSharpBinderFlags.None, mPropertyInfo.Name, thisObject.GetType(),
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
            var callsite = CallSite<Func<CallSite, object, object>>.Create(binder);
            return callsite.Target(callsite, thisObject);
        }

        public void SetValue(object thisObject, object value)
        {
            var binder = Binder.SetMember(CSharpBinderFlags.None, mPropertyInfo.Name, thisObject.GetType(),
                new[] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                    CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) });
            var callsite = CallSite<Func<CallSite, object, object, object>>.Create(binder);
            callsite.Target(callsite, thisObject, value);
        }

        public T GetCustomAttribute<T>() where T : Attribute
        {
            if (typeof(T) == typeof(EntityPropertyAttribute))
                return mPropertyInfo.EntityPropertyAttribute as T;
            if (typeof(T) == typeof(ObsoleteEntityPropertyAttribute))
                return mPropertyInfo.ObsoleteEntityPropertyAttribute as T;
            return null;
        }
    }
}
using System;
using System.Reflection;

namespace Gehtsoft.EF.Mapper
{
    public class ClassPropertyAccessor : IMappingSource, IMappingTarget
    {
        private readonly PropertyInfo mPropertyInfo;

        public PropertyInfo PropertyInfo => mPropertyInfo;

        public ClassPropertyAccessor(PropertyInfo propertyInfo)
        {
            mPropertyInfo = propertyInfo;
        }

        public string Name => mPropertyInfo.Name;
        public Type ValueType => mPropertyInfo.PropertyType;
        public void Set(object obj, object value) => mPropertyInfo.SetValue(obj, value);
        public object Get(object obj) => mPropertyInfo.GetValue(obj);

        public bool Equals(IMappingTarget target) => Equals((object) target);

        protected bool Equals(ClassPropertyAccessor other)
        {
            return Equals(mPropertyInfo, other.mPropertyInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClassPropertyAccessor) obj);
        }

        public override int GetHashCode()
        {
            return (mPropertyInfo != null ? mPropertyInfo.GetHashCode() : 0);
        }
    }
}
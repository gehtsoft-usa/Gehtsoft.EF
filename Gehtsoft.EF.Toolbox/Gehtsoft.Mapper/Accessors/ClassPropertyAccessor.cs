using System;
using System.Reflection;

namespace Gehtsoft.EF.Mapper
{
    public sealed class ClassPropertyAccessor : IMappingSource, IMappingTarget
    {
        public PropertyInfo PropertyInfo { get; }

        public ClassPropertyAccessor(PropertyInfo propertyInfo)
        {
            PropertyInfo = propertyInfo;
        }

        public string Name => PropertyInfo.Name;
        public Type ValueType => PropertyInfo.PropertyType;
        public void Set(object obj, object value) => PropertyInfo.SetValue(obj, value);
        public object Get(object obj) => PropertyInfo.GetValue(obj);

        public bool Equals(IMappingTarget target) => Equals((object)target);

        private bool Equals(ClassPropertyAccessor other)
        {
            return Equals(PropertyInfo, other.PropertyInfo);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ClassPropertyAccessor)obj);
        }

        public override int GetHashCode()
        {
            return (PropertyInfo != null ? PropertyInfo.GetHashCode() : 0);
        }
    }
}
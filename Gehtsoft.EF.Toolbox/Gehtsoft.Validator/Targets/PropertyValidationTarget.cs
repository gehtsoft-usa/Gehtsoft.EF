using System;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public class PropertyValidationTarget : ValidationTarget
    {
        public override string PropertyName { get; }
        private PropertyInfo mPropertyInfo = null;

        public override Type ValueType => mPropertyInfo.PropertyType;
        public override string TargetName => mPropertyInfo.Name;
        public override bool IsProperty => true;

        public override T GetCustomAttribute<T>() => mPropertyInfo.GetCustomAttribute<T>();


        public PropertyValidationTarget(Type type, string propertyName)
        {
            PropertyName = propertyName;
            mPropertyInfo = type.GetTypeInfo().GetProperty(propertyName);
        }


        public override bool IsSingleValue => true;

        public override ValidationValue First(object target) => new ValidationValue() {Name = PropertyName, Value = mPropertyInfo.GetValue(target)};

        public override ValidationValue[] All(object target)
        {
            return new ValidationValue[] { First(target) };
        }
    }

}

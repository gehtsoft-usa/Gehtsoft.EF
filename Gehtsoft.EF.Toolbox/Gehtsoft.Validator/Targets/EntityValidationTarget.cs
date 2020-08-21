using System;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public class EntityValidationTarget : ValidationTarget
    {
        private Type mType;
        private string mName;

        public override Type ValueType => mType;
        public override string TargetName => mName;
        
        public override T GetCustomAttribute<T>() => mType.GetTypeInfo().GetCustomAttribute<T>();


        public EntityValidationTarget(Type type, string name)
        {
            mName = name;
            mType = type;
        }


        public override bool IsSingleValue => true;

        public override ValidationValue First(object target) => new ValidationValue() {Name = mName, Value = target};

        public override ValidationValue[] All(object target)
        {
            return new ValidationValue[] { First(target) };
        }

        public override bool IsProperty => false;

        public override string PropertyName => null;
    }
}
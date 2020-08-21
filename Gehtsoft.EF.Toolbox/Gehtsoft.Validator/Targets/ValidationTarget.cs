using System;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public interface IValidationTarget
    {
        string TargetName { get; }
        Type ValueType { get; }
        bool IsSingleValue { get; }
    }


    public abstract class ValidationTarget : IValidationTarget
    {
        public class ValidationValue
        {
            public object Value { get; set; }
            public string Name { get; set; }
        }
        public abstract string TargetName { get; }
        public abstract Type ValueType { get; }
        public abstract bool IsSingleValue { get; }
        public abstract ValidationValue First(object target);
        public abstract ValidationValue[] All(object target);
        public abstract T GetCustomAttribute<T>() where T : Attribute;
        public abstract bool IsProperty { get;  }
        public abstract string PropertyName { get;  }
    }
}

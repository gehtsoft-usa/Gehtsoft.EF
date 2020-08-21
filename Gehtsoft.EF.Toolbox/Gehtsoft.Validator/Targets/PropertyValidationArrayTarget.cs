using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public class PropertyValidationArrayTarget : ValidationTarget
    {
        public override string PropertyName { get; }
        private PropertyInfo mPropertyInfo = null;
        private Type mElementType = null;

        public override Type ValueType => mElementType;
        public override string TargetName => mPropertyInfo.Name;
        public override T GetCustomAttribute<T>() => mPropertyInfo.GetCustomAttribute<T>();
        public override bool IsProperty => true;
        
        public PropertyValidationArrayTarget(Type type, string propertyName)
        {
            PropertyName = propertyName;
            mPropertyInfo = type.GetTypeInfo().GetProperty(propertyName);
            TypeInfo typeInfo = mPropertyInfo.PropertyType.GetTypeInfo();

            if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                mElementType = typeInfo.GetGenericArguments()[0];
            else
            {               
                foreach (Type interfaceType in typeInfo.ImplementedInterfaces)
                {
                    TypeInfo interfaceTypeInfo = interfaceType.GetTypeInfo();
                    if (interfaceTypeInfo.IsGenericType && interfaceTypeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        mElementType = interfaceTypeInfo.GetGenericArguments()[0];
                        break;
                    }

                }
                if (mElementType == null)
                    throw new ArgumentException("The property is not a enumerable");
            }
        }


        public override bool IsSingleValue => false;

        private IEnumerable GetEnumerable(object target)
        {
            object container = mPropertyInfo.GetValue(target);
            if (container == null)
                return null;
            IEnumerable enumerable = container as IEnumerable;
            if (enumerable == null)
                return null;
            return enumerable;
        }

        public override ValidationValue First(object target)
        {
            IEnumerator enumerator = GetEnumerable(target).GetEnumerator();
            if (enumerator == null)
                return new ValidationValue() {Value = null, Name = $"{PropertyName}[{0}]"};
            enumerator.Reset();
            if (!enumerator.MoveNext())
                throw new InvalidOperationException("Value is empty");
            return new ValidationValue() {Value = enumerator.Current, Name = $"{PropertyName}[{0}]"};

        }

        public override ValidationValue[] All(object target)
        {
            IEnumerable enumerable = GetEnumerable(target);
            int i = 0;
            List<ValidationValue> result = new List<ValidationValue>();
            if (enumerable != null)
            {
                foreach (object x in enumerable)
                    result.Add(new ValidationValue() {Name = $"{PropertyName}[{i++}]", Value = x});
            }
            return result.ToArray();
        }
    }
}

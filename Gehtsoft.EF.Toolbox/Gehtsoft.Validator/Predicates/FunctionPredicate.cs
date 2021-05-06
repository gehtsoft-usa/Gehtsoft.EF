using System;
using System.Reflection;

namespace Gehtsoft.Validator
{
    public class FunctionPredicate<T> : IValidationPredicate
    {
        private readonly Func<T, bool> mPredicate;
        private readonly bool IsTValue;
        private readonly bool IsTEnum;
        private readonly bool IsTNullable;
        private readonly Type mParameterType;
        private readonly Type mCoversionType;

        public Type ParameterType => mParameterType;

        public FunctionPredicate(Func<T, bool> predicate)
        {
            mPredicate = predicate;
            Type t = typeof(T);
            TypeInfo typeInfo = t.GetTypeInfo();
            IsTValue = typeInfo.IsValueType;
            Type t1 = Nullable.GetUnderlyingType(t);
            IsTNullable = t1 != t;
            mParameterType = typeof(T);
            mCoversionType = Nullable.GetUnderlyingType(mParameterType) ?? mParameterType;
            IsTEnum = mCoversionType.GetTypeInfo().IsEnum;
        }

        public bool Validate(object value)
        {
            if (value == null)
            {
                if (IsTValue)
                    value = Activator.CreateInstance(typeof(T));
            }
            else
            {
                Type t = value.GetType();
                if (t != typeof(T))
                {
                    Type t1 = Nullable.GetUnderlyingType(t);

                    if (t1 != t && t1 != null)
                        value = Convert.ChangeType(value, t1);

                    if (IsTEnum)
                        value = Enum.ToObject(mCoversionType, value);

                    if (mCoversionType != typeof(object))
                    {
                        if (mCoversionType == typeof(string))
                            value = value.ToString();
                        else
                            value = Convert.ChangeType(value, mCoversionType);
                    }
                }
            }
            return mPredicate((T)value);
        }

        public virtual string RemoteScript(Type compilerType) => null;
    }
}
using System;

namespace Gehtsoft.Validator
{
    public class IsEnumValueCorrectPredicate : IValidationPredicate
    {
        public IsEnumValueCorrectPredicate(Type parameterType)
        {
            Type t = Nullable.GetUnderlyingType(parameterType);
            if (t != null && t != parameterType)
                parameterType = t;
            mParameterType = parameterType;
        }

        private readonly Type mParameterType;
        public Type ParameterType => mParameterType;
        public bool Validate(object value) => value != null && Enum.IsDefined(ParameterType, value);
        public string RemoteScript(Type compilerType) => null;
    }
}

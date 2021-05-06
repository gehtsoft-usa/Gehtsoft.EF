using System;

namespace Gehtsoft.Validator
{
    public class IsNullPredicate : IValidationPredicate
    {
        private readonly Type mParameterType;
        public Type ParameterType => mParameterType;

        public IsNullPredicate(Type parameterType)
        {
            mParameterType = parameterType;
        }

        public bool Validate(object value) => value == null;

        public string RemoteScript(Type compilerType) => "jsv_isempty(value)";
    }
}
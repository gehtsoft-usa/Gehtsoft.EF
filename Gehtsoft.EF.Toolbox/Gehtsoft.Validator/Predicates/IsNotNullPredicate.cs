using System;

namespace Gehtsoft.Validator
{
    public class IsNotNullPredicate : IValidationPredicate
    {
        public IsNotNullPredicate(Type parameterType)
        {
            mParameterType = parameterType;
        }

        private Type mParameterType;
        public Type ParameterType => mParameterType;
        public bool Validate(object value) => value != null;

        public string RemoteScript(Type compilerType) => "(value != null && value != undefined)";

    }
}
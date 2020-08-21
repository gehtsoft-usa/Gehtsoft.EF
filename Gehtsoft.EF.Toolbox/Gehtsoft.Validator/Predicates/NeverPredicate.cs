using System;

namespace Gehtsoft.Validator
{
    public class NeverPredicate : IValidationPredicate
    {
        private Type mParameterType;
        public Type ParameterType => mParameterType;

        public NeverPredicate(Type parameterType)
        {
            mParameterType = parameterType;
        }

        public bool Validate(object value) => false;

        public string RemoteScript(Type compilerType) => "false";
    }
}
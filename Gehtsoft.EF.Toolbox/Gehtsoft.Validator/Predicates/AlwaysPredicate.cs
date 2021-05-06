using System;

namespace Gehtsoft.Validator
{
    public class AlwaysPredicate : IValidationPredicate
    {
        private readonly Type mParameterType;
        public Type ParameterType => mParameterType;

        public AlwaysPredicate(Type parameterType)
        {
            mParameterType = parameterType;
        }

        public bool Validate(object value) => true;

        public string RemoteScript(Type compilerType) => "true";
    }
}
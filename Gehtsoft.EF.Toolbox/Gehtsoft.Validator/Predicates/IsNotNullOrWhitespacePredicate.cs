using System;

namespace Gehtsoft.Validator
{
    public class IsNotNullOrWhitespacePredicate : IValidationPredicate
    {
        public IsNotNullOrWhitespacePredicate(Type parameterType)
        {
            mParameterType = parameterType;
        }

        private Type mParameterType;
        public Type ParameterType => mParameterType;
        public bool Validate(object value) => !string.IsNullOrWhiteSpace(value?.ToString());

        public string RemoteScript(Type compilerType) => "!jsv_isemptyorwhitespace(value)";

    }
}
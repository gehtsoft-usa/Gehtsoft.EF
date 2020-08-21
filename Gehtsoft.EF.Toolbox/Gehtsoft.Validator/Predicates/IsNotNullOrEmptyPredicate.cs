using System;
using System.Collections;

namespace Gehtsoft.Validator
{
    public class IsNotNullOrEmptyPredicate : IValidationPredicate
    {
        public IsNotNullOrEmptyPredicate(Type parameterType)
        {
            mParameterType = parameterType;
        }

        private Type mParameterType;
        public Type ParameterType => mParameterType;
        public bool Validate(object value)
        {
            if (value == null)
                return false;

            if (value is Array array)
            {
                return array.Length > 0;
            }
            
            if (value is ICollection collection)
            {
                return collection.Count > 0;
            }

            if (!(value is string) && value is IEnumerable)
            {
                IEnumerator e = (value as IEnumerable).GetEnumerator();
                return e.MoveNext();
            }

            return value.ToString().Length > 0;
        }

        public string RemoteScript(Type compilerType) => "!jsv_isempty(value)";
    }
}
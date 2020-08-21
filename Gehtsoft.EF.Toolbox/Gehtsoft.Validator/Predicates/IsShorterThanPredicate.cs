using System;
using System.Collections;
using Gehtsoft.ExpressionToJs;

namespace Gehtsoft.Validator
{
    public class IsShorterThanPredicate : IValidationPredicate
    {
        private int mLength;

        public IsShorterThanPredicate(Type parameterType, int length)
        {
            mParameterType = parameterType;
            mLength = length;
        }

        private Type mParameterType;
        public Type ParameterType => mParameterType;
        public bool Validate(object value)
        {
            if (value == null)
                return true;

            if (value is Array array)
            {
                return array.Length < mLength;
            }
            
            if (value is ICollection collection)
            {
                return collection.Count < mLength;
            }

            return value.ToString().Length < mLength;
        }

        public string RemoteScript(Type compilerType) => $"jsv_less(jsv_length(value),{ExpressionCompiler.AddConstant(mLength)})";
    }
}

using System;
using System.Globalization;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Validator
{
    public class DecimalPropertyRangePredicate : IValidationPredicate
    {
        private readonly int mSize, mPrecision;
        private readonly decimal mMax;

        public DecimalPropertyRangePredicate(int size, int precision)
        {
            mSize = size;
            mPrecision = precision;
            mMax = (decimal)Math.Pow(10, size - precision);
        }

        public Type ParameterType => typeof(decimal);

        public bool Validate(object value)
        {
            if (value == null)
                return true;

            Type type = value.GetType();
            Type type1 = Nullable.GetUnderlyingType(type);
            if (type != type1 && type1 != null)
                value = Convert.ChangeType(value, type1);

            decimal v;
            if (value is decimal x)
                v = x;
            else
                v = (decimal)Convert.ChangeType(value, typeof(decimal));

            return v > -mMax && v < mMax;
        }

        public string RemoteScript(Type compilerType) => $"jsv_and(jsv_less(value, {mMax.ToString(CultureInfo.InvariantCulture)}), jsv_greater(value, {(-mMax).ToString(CultureInfo.InvariantCulture)}))";
    }
}
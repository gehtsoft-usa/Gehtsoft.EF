using System;
using System.Globalization;
using Gehtsoft.Validator;

namespace Gehtsoft.EF.Validator
{
    public class NumberPropertyRangePredicate : IValidationPredicate
    {
        private int mSize, mPrecision;
        private double mMax;

        public NumberPropertyRangePredicate(int size, int precision)
        {
            mSize = size;
            mPrecision = precision;
            mMax = Math.Pow(10, size - precision);
        }

        public Type ParameterType => typeof(double);

        public bool Validate(object value)
        {
            if (value == null)
                return true;

            Type type = value.GetType();
            Type type1 = Nullable.GetUnderlyingType(type);
            if (type != type1 && type1 != null)
                value = Convert.ChangeType(value, type1);

            double v;
            if (value is double)
                v = (double) value;
            else
                v = (double) Convert.ChangeType(value, typeof(double));

            return v > -mMax && v < mMax;
        }

        public string RemoteScript(Type compilerType) => $"jsv_and(jsv_less(value, {mMax.ToString(CultureInfo.InvariantCulture)}), jsv_greater(value, {(-mMax).ToString(CultureInfo.InvariantCulture)}))";
    }
}

using System;

namespace Gehtsoft.Validator
{
    public class ValueIsBetweenPredicate : IValidationPredicate
    {
        private readonly IComparable mMin, mMax;
        private readonly bool mMinInclusive, mMaxInclusive;
        public Type ParameterType { get; }
        private readonly string mJavaScript;

        public ValueIsBetweenPredicate(Type parameterType, object min, bool minInclusive, object max, bool maxInclusive)
        {
            ParameterType = parameterType;
            mMin = min as IComparable;
            mMax = max as IComparable;
            mMinInclusive = minInclusive;
            mMaxInclusive = maxInclusive;

            try
            {
                string
                    jsMin = mMin == null ? "true" : $"jsv_{(mMinInclusive ? "greaterorequal" : "greater")}(value, {ExpressionToJs.ExpressionCompiler.AddConstant(mMin)})",
                    jsMax = mMax == null ? "true" : $"jsv_{(mMaxInclusive ? "lessorequal" : "less")}(value, {ExpressionToJs.ExpressionCompiler.AddConstant(mMax)})";

                mJavaScript = $"jsv_and({jsMin}, {jsMax})";
            }
            catch (Exception)
            {
                mJavaScript = null;
            }
        }

        public bool Validate(object value)
        {
            if (mMin != null)
            {
                int rc = mMin.CompareTo(value);
                if (mMinInclusive)
                {
                    if (rc > 0)
                        return false;
                }
                else
                {
                    if (rc >= 0)
                        return false;
                }
            }
            if (mMax != null)
            {
                int rc = mMax.CompareTo(value);
                if (mMaxInclusive)
                {
                    if (rc < 0)
                        return false;
                }
                else
                {
                    if (rc <= 0)
                        return false;
                }
            }

            return true;
        }

        public string RemoteScript(Type compilerType) => mJavaScript;
    }
}
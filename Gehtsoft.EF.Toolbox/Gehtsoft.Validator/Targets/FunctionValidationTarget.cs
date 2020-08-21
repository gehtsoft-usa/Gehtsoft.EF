using System;
using System.Linq.Expressions;
using System.Reflection;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.Validator
{
    public class FunctionValidationTarget<TE, TV> : ValidationTarget
    {
        private Func<TE, TV> mPredicate;
        private string mName;
        private PropertyInfo mPropertyInfo;

        public override string TargetName => mName;

        public override T GetCustomAttribute<T>() => mPropertyInfo?.GetCustomAttribute<T>();
        public override bool IsProperty => mPropertyInfo != null;
        public override string PropertyName => mPropertyInfo?.Name;

        public FunctionValidationTarget(Expression<Func<TE, TV>> predicate, string name)
        {
            mName = name ?? ExpressionUtils.ExpressionToName(predicate);
            try
            {
                mPropertyInfo = ExpressionUtils.ExpressionToMemberInfo(predicate) as PropertyInfo;
            }
            catch (Exception)
            {
                mPropertyInfo = null;
            }

            mPredicate = predicate.Compile();
        }

        public override Type ValueType => typeof(TV);

        public override bool IsSingleValue => true;

        public override ValidationValue First(object target) => new ValidationValue() {Name = mName, Value = mPredicate((TE)target)};

        public override ValidationValue[] All(object target) => new ValidationValue[] {First(target)};
    }
}

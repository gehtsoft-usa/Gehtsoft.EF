using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.Validator
{
    public class FunctionValidationArrayTarget<TE, TV> : ValidationTarget where TV : IEnumerable
    {
        private readonly Func<TE, TV> mPredicate;
        private readonly string mName;
        private readonly Type mElementType;
        private readonly PropertyInfo mPropertyInfo;

        public override string TargetName => mName;
        public override T GetCustomAttribute<T>() => mPropertyInfo?.GetCustomAttribute<T>();

        public override bool IsProperty => mPropertyInfo != null;
        public override string PropertyName => mPropertyInfo?.Name;

        public FunctionValidationArrayTarget(Expression<Func<TE, TV>> predicate, string name)
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
            TypeInfo typeInfo = typeof(TV).GetTypeInfo();

            if (typeInfo.IsGenericType && typeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                mElementType = typeInfo.GetGenericArguments()[0];
            else
            {
                foreach (Type interfaceType in typeInfo.ImplementedInterfaces)
                {
                    TypeInfo interfaceTypeInfo = interfaceType.GetTypeInfo();
                    if (interfaceTypeInfo.IsGenericType && interfaceTypeInfo.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        mElementType = interfaceTypeInfo.GetGenericArguments()[0];
                        break;
                    }
                }
                if (mElementType == null)
                    throw new ArgumentException("The property is not a enumerable");
            }
        }

        public override Type ValueType => mElementType;

        public override bool IsSingleValue => false;

        private IEnumerable GetEnumerable(object target)
        {
            IEnumerable enumerable = mPredicate((TE)target);
            if (enumerable == null)
                return null;
            return enumerable;
        }

        public override ValidationValue First(object target)
        {
            IEnumerator enumerator = GetEnumerable(target).GetEnumerator();
            if (enumerator == null)
                return new ValidationValue() { Value = null, Name = $"{mName}[{0}]" };
            enumerator.Reset();
            if (!enumerator.MoveNext())
                throw new InvalidOperationException("Value is empty");
            return new ValidationValue() { Value = enumerator.Current, Name = $"{mName}[{0}]" };
        }

        public override ValidationValue[] All(object target)
        {
            IEnumerable enumerable = GetEnumerable(target);
            int i = 0;
            List<ValidationValue> result = new List<ValidationValue>();
            if (enumerable != null)
            {
                foreach (object x in enumerable)
                    result.Add(new ValidationValue() { Name = $"{mName}[{i++}]", Value = x });
            }

            return result.ToArray();
        }
    }
}
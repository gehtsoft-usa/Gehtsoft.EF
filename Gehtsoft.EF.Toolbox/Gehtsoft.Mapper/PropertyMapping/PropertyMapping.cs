using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.Mapper
{
    public class PropertyMapping<TSource, TDestination> : IPropertyMapping 
    {
        public IMappingTarget Target { get; set; }
        public IMappingSource Source { get; set; }
        public IMappingPredicate WhenPredicate { get; set; }
        public MapFlag MapFlag { get; set; }

        private readonly Map<TSource, TDestination> mParentMap;

        public PropertyMapping(Map<TSource, TDestination> map)
        {
            mParentMap = map;
        }

        public PropertyMapping<TSource, TDestination> From(IMappingSource source)
        {
            Source = source;
            return this;
        }

        internal static PropertyInfo PropertyOfParameterInfo(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Lambda)
                return PropertyOfParameterInfo(((LambdaExpression) expression).Body);

            if (expression.NodeType == ExpressionType.Convert)
                return PropertyOfParameterInfo(((UnaryExpression) expression).Operand);

            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression e = (MemberExpression) expression;
                MemberInfo mi = e.Member;
                if (mi.MemberType == MemberTypes.Property && e.Expression.NodeType == ExpressionType.Parameter)
                    return (PropertyInfo) mi;
            }

            return null;
        }

        public PropertyMapping<TSource, TDestination> From<TValue>(Expression<Func<TSource, TValue>> from)
        {
            PropertyInfo memberInfo = null;

            try
            {
                memberInfo = PropertyOfParameterInfo(from);
            }
            catch (Exception)
            {
                ;
            }

            PropertyInfo propertyInfo = memberInfo as PropertyInfo;

            if (propertyInfo != null && typeof(TSource).IsAssignableFrom(propertyInfo.DeclaringType))
                Source = new ClassPropertyAccessor(propertyInfo);
            else
                Source = new ExpressionSource<TSource, TValue>(from.Compile());

            return this;
        }

        public PropertyMapping<TSource, TDestination> From(string propertyName)
        {
            PropertyInfo propertyInfo = typeof(TSource).GetTypeInfo().GetProperty(propertyName);
            if (propertyInfo == null)
                throw new ArgumentException("Property isn't found", nameof(propertyName));
            Source = new ClassPropertyAccessor(propertyInfo);
            return this;
        }



        public PropertyMapping<TSource, TDestination> To(IMappingTarget target)
        {
            Target = target;
            return this;
        }

        public PropertyMapping<TSource, TDestination> To(string propertyName)
        {
            PropertyInfo propertyInfo = typeof(TDestination).GetTypeInfo().GetProperty(propertyName);
            if (propertyInfo == null)
                throw new ArgumentException("Property isn't found", nameof(propertyName));
            Target = new ClassPropertyAccessor(propertyInfo);
            return this;
        }

        public PropertyMapping<TSource, TDestination> To<TValue>(Expression<Func<TDestination, TValue>> to)
        {
            PropertyInfo memberInfo = null;
            try
            {
                memberInfo = PropertyOfParameterInfo(to);
            }
            catch (Exception)
            {
                ;
            }

            PropertyInfo propertyInfo = memberInfo as PropertyInfo;
            if (propertyInfo == null)
                throw new ArgumentException("The expression is not a simple property access expression!", nameof(to));

            Target = new ClassPropertyAccessor(propertyInfo);
            return this;
        }

        public PropertyMapping<TSource, TDestination> To<TValue>(Action<TDestination, TValue> action)
        {
            Target = new ActionTarget<TDestination, TValue>(action);
            return this;
        }

        public PropertyMapping<TSource, TDestination> Assign<TValue>(TValue constant)
        {
            Source = new ConstSource<TValue>(constant);
            return this;
        }

        public PropertyMapping<TSource, TDestination> Assign<TValue>(Func<TSource, TValue> func)
        {
            Source = new ExpressionSource<TSource, TValue>(func);
            return this;
        }

        public PropertyMapping<TSource, TDestination> When(IMappingPredicate predicate)
        {
            WhenPredicate = predicate;
            return this;
        }


        public PropertyMapping<TSource, TDestination> When(Func<TSource, bool> predicate)
        {
            WhenPredicate = new MappingPredicate<TSource>(predicate);
            return this;
        }

        public PropertyMapping<TSource, TDestination> WhenDestination(Func<TDestination, bool> predicate)
        {
            WhenPredicate = new MappingPredicate<TDestination>(predicate);
            return this;
        }


        public PropertyMapping<TSource, TDestination> Ignore()
        {
            WhenPredicate = new NeverMappingPredicate();
            return this;
        }

        public PropertyMapping<TSource, TDestination> ReplaceWith()
        {
            mParentMap.FindTarget(Target).Ignore();
            return mParentMap.AddTarget(Target);
        }

        public PropertyMapping<TSource, TDestination> Always()
        {
            WhenPredicate = null;
            return this;
        }

        public PropertyMapping<TSource, TDestination> WithFlags(MapFlag flag)
        {
            MapFlag = flag;
            return this;
        }

        public PropertyMapping<TSource, TDestination> Otherwise()
        {
            PropertyMapping<TSource, TDestination> newMapping = mParentMap.AddTarget(Target);
            if (WhenPredicate == null)
                newMapping.WhenPredicate = new NeverMappingPredicate();
            else
                newMapping.WhenPredicate = new NotMappingPredicate(WhenPredicate);
            return newMapping;
        }

        public void Map(TSource source, TDestination destination) => Map(source, destination, false);

        public void Map(TSource source, TDestination destination, bool ignoreNull)
        {
            bool proceed = true;
            if (WhenPredicate != null)
            {
                if (WhenPredicate.ParameterType == null)
                    proceed = WhenPredicate.Evaluate(null);
                else if (WhenPredicate.ParameterType == typeof(TSource))
                    proceed = WhenPredicate.Evaluate(source);
                else if (WhenPredicate.ParameterType == typeof(TDestination))
                    proceed = WhenPredicate.Evaluate(destination);
            }

            if (proceed && Source != null && Target != null)
            {
                object sourceValue = Source.Get(source);
                if (ignoreNull && sourceValue == null)
                    return;
                object value = ValueMapper.MapValue(sourceValue, Target.ValueType, MapFlag);
                Target.Set(destination, value);
            }
        }

        public void Map(object source, object destination) => Map((TSource) source, (TDestination) destination, false);

        public void Map(object source, object destination, bool ignoreNull) => Map((TSource) source, (TDestination) destination, ignoreNull);
    }

    public static class EnumerableOfPropertyMappingExtension
    {
        public static void Ignore<TSource, TDestination>(this IEnumerable<PropertyMapping<TSource, TDestination>> mappings) => mappings.ForEach(mapping => mapping.Ignore());
    }
}

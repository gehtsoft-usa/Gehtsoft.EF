using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.Mapper
{
    public class Map<TSource, TDestination> : IMap
    {
        public PropertyMappingCollection<TSource, TDestination> Mappings { get; } = new PropertyMappingCollection<TSource, TDestination>();

        IPropertyMappingCollection IMap.Mappings => Mappings;

        public Type Source => typeof(TSource);

        public Type Destination => typeof(TDestination);

        private readonly MappingActionCollection<TSource, TDestination> mPre = new MappingActionCollection<TSource, TDestination>();

        private readonly MappingActionCollection<TSource, TDestination> mPost = new MappingActionCollection<TSource, TDestination>();

        IMappingActionCollection IMap.Pre => mPre;

        IMappingActionCollection IMap.Post => mPost;

        public bool MapNullToNull { get; set; } = true;

        public Map()
        {
        }

        public virtual Func<TSource, TDestination> Factory { get; set; } = null;

        private object FactoryFunction(object source)
        {
            if (Factory == null)
                return null;
            return Factory((TSource)source);
        }

        Func<object, object> IMap.Factory
        {
            get
            {
                if (Factory == null)
                    return null;
                else
                    return FactoryFunction;
            }
        }

        protected internal virtual PropertyMapping<TSource, TDestination> AddTarget(IMappingTarget target)
        {
            PropertyMapping<TSource, TDestination> mapping = new PropertyMapping<TSource, TDestination>(this)
            {
                Target = target
            };
            Mappings.Add(mapping);
            return mapping;
        }

        public virtual IEnumerable<PropertyMapping<TSource, TDestination>> FindTarget(IMappingTarget target)
        {
            foreach (PropertyMapping<TSource, TDestination> mapping in Mappings)
                if (mapping.Target.Equals(target))
                    yield return mapping;
        }

        public virtual PropertyMapping<TSource, TDestination> For(string name)
        {
            PropertyInfo propertyInfo = typeof(TDestination).GetTypeInfo().GetProperty(name);
            if (propertyInfo == null)
                throw new ArgumentException("Property is not found", nameof(name));
            IMappingTarget target = new ClassPropertyAccessor(propertyInfo);
            return AddTarget(target);
        }

        public virtual PropertyMapping<TSource, TDestination> For<TValue>(Expression<Func<TDestination, TValue>> member)
        {
            MemberInfo memberInfo = PropertyMapping<TSource, TDestination>.PropertyOfParameterInfo(member);

            if (!(memberInfo is PropertyInfo propertyInfo))
                throw new InvalidOperationException("The expression is not a simple property access expression!");

            IMappingTarget target = new ClassPropertyAccessor(propertyInfo);
            return AddTarget(target);
        }

        protected virtual IMappingTarget GetTargetByName(string name)
        {
            IMappingTarget target = null;

            PropertyInfo propertyInfo = typeof(TDestination).GetTypeInfo().GetProperty(name);
            if (propertyInfo != null)
                target = new ClassPropertyAccessor(propertyInfo);

            return target;
        }

        public virtual IEnumerable<PropertyMapping<TSource, TDestination>> Find(string name)
        {
            IMappingTarget target = GetTargetByName(name);

            if (target == null)
                yield break;

            foreach (var t in FindTarget(target))
                yield return t;
        }

        public virtual bool ContainsRuleFor(string name)
        {
            IMappingTarget target = GetTargetByName(name);

            if (target == null)
                return false;

            var t = FindTarget(target);
            return t.Any();
        }

        public virtual IEnumerable<PropertyMapping<TSource, TDestination>> Find<TValue>(Expression<Func<TDestination, TValue>> member)
        {
            MemberInfo memberInfo = PropertyMapping<TSource, TDestination>.PropertyOfParameterInfo(member);

            if (!(memberInfo is PropertyInfo propertyInfo))
                throw new InvalidOperationException("The expression is not a simple property access expression!");

            IMappingTarget target = GetTargetByName(propertyInfo.Name);

            if (target == null)
                yield break;

            foreach (var item in FindTarget(target))
                yield return item;
        }

        public virtual PropertyMapping<TSource, TDestination> Assign<TValue>(Action<TDestination, TValue> action)
        {
            IMappingTarget target = new ActionTarget<TDestination, TValue>(action);
            return AddTarget(target);
        }

        public virtual MappingAction<TSource, TDestination> BeforeMapping(Action<TSource, TDestination> action)
        {
            MappingAction<TSource, TDestination> ma = new MappingAction<TSource, TDestination>(action);
            mPre.Add(ma);
            return ma;
        }

        public virtual MappingAction<TSource, TDestination> AfterMapping(Action<TSource, TDestination> action)
        {
            MappingAction<TSource, TDestination> ma = new MappingAction<TSource, TDestination>(action);
            mPost.Add(ma);
            return ma;
        }

        protected virtual bool Equals(Map<TSource, TDestination> other)
        {
            return Equals((object)other);
        }

        public override bool Equals(object obj) => Equals(obj as IMap);

        public virtual bool Equals(IMap obj)
        {
            if (obj is null) return false;
            return ReferenceEquals(this, obj) || (Source == obj.Source && Destination == obj.Destination);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return this.Source.GetHashCode() ^ 397 ^ this.Destination.GetHashCode();
            }
        }

        public virtual void Do(TSource source, TDestination destination, bool ignoreNull)
        {
            foreach (IMappingAction predicate in mPre)
                predicate.Perform(source, destination);

            foreach (IPropertyMapping mapping in Mappings)
                mapping.Map(source, destination, ignoreNull);

            foreach (IMappingAction predicate in mPost)
                predicate.Perform(source, destination);
        }

        public virtual void Do(TSource source, TDestination destination) => Do(source, destination, false);

        public virtual TDestination Do(TSource source)
        {
            if (source == null && MapNullToNull)
                return default;

            TDestination destination;
            if (Factory != null)
                destination = Factory(source);
            else if (source == null)
                return default;
            else
                destination = Activator.CreateInstance<TDestination>();
            Do(source, destination);
            return destination;
        }

        public virtual object Do(object from) => Do((TSource)from);

        IPropertyMapping IMap.For(IMappingTarget source) => AddTarget(source);

        void IMap.Do(object from, object to) => Do((TSource)from, (TDestination)to);

        void IMap.Do(object from, object to, bool ignoreNull) => Do((TSource)from, (TDestination)to);

        public virtual IEnumerator<IPropertyMapping> GetEnumerator()
        {
            return Mappings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class MapExtension
    {
        public static void MapPropertiesByName<TSource, TDestination>(this Map<TSource, TDestination> map, bool onlyValueTypes = false, string[] propertyIgnoreList = null, Type[] typeIgnoreList = null)
        {
            Type sourceType = typeof(TSource);
            Type destinationType = typeof(TDestination);

            foreach (PropertyInfo sourceProperty in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (sourceProperty.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                    continue;

                PropertyInfo destinationProperty = destinationType.GetProperty(sourceProperty.Name, BindingFlags.Public | BindingFlags.Instance);

                if (destinationProperty == null)
                    continue;

                if (destinationProperty.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                    continue;

                if (onlyValueTypes && (!sourceProperty.PropertyType.IsMappingValueType() || !destinationProperty.PropertyType.IsMappingValueType()))
                    continue;

                if (propertyIgnoreList != null && (propertyIgnoreList.Contains(sourceProperty.Name) || propertyIgnoreList.Contains(destinationProperty.Name)))
                    continue;

                if (typeIgnoreList != null && (typeIgnoreList.Contains(sourceProperty.PropertyType) || typeIgnoreList.Contains(destinationProperty.PropertyType)))
                    continue;

                if (!map.ContainsRuleFor(destinationProperty.Name))
                    map.For(destinationProperty.Name).From(sourceProperty.Name);
            }
        }

        private static bool IsMappingValueType(this Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(string) || type.IsValueType || type.IsEnum;
        }
    }
}

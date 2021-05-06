using System;
using System.Collections.Generic;
using System.Reflection;

namespace Gehtsoft.EF.Mapper
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DoNotAutoMapAttribute : Attribute
    {
    }

    public static class MapFactory
    {
        private static readonly Dictionary<Tuple<Type, Type>, IMap> mMaps = new Dictionary<Tuple<Type, Type>, IMap>();

        private static IMap FindMap(Type from, Type to)
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(from, to);

            if (mMaps.TryGetValue(key, out IMap map))
                return map;
            return null;
        }

        private static IMap Construct(Type from, Type to)
        {
            Type mapType = typeof(Map<,>).MakeGenericType(new Type[] { from, to });
            IMap map = Activator.CreateInstance(mapType) as IMap;
            return map;
        }

        public static IMap GetMap(Type from, Type to, bool createIfNotExist = true)
        {
            IMap map = FindMap(from, to);
            if (map != null)
                return map;
            if (createIfNotExist)
                return CreateMap(from, to, false);
            else
                return null;
        }

        public static bool HasMap(Type from, Type to) => FindMap(from, to) != null;

        public static bool HasMap<TFrom, TTo>() => HasMap(typeof(TFrom), typeof(TTo));

        public static Map<TFrom, TTo> GetMap<TFrom, TTo>(bool createIfNotExist = true)
        {
            return (Map<TFrom, TTo>)GetMap(typeof(TFrom), typeof(TTo), createIfNotExist);
        }

        public static Map<TFrom, TTo> CreateMap<TFrom, TTo>()
        {
            return (Map<TFrom, TTo>)CreateMap(typeof(TFrom), typeof(TTo));
        }

        public static IMap CreateMap(Type from, Type to, bool createAlways = true)
        {
            IMap map = Construct(from, to);

            //check whether we can auto-create the map using attributes
            if (from.GetTypeInfo().GetCustomAttribute<MapSpecificationAttribute>() != null)
                from.GetTypeInfo().GetCustomAttribute<MapSpecificationAttribute>().GetInitializer().ModelToSource(map);
            else if (to.GetTypeInfo().GetCustomAttribute<MapSpecificationAttribute>() != null)
                to.GetTypeInfo().GetCustomAttribute<MapSpecificationAttribute>().GetInitializer().SourceToModel(map);
            else if (!createAlways)
                throw new ArgumentException($"A map between {from.Name} and {to.Name} cannot be created automatically");

            mMaps[new Tuple<Type, Type>(from, to)] = map;
            return map;
        }

        public static void Map<TFrom, TTo>(TFrom source, TTo destination)
        {
            ValueMapper.MapValue(source, typeof(TTo), MapFlag.None, destination);
        }

        public static TTo Map<TFrom, TTo>(TFrom source)
        {
            IMap map = FindMap(typeof(TFrom), typeof(TTo));
            if (map != null)
                return (TTo)map.Do(source);

            return (TTo)ValueMapper.MapValue(source, typeof(TTo));
        }

        public static void RemoveMap(Type source, Type destination)
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(source, destination);
            if (mMaps.ContainsKey(key))
                mMaps.Remove(key);
        }

        public static void RemoveMap<TFrom, TTo>() => RemoveMap(typeof(TFrom), typeof(TTo));
    }

    public class ClassToModelInitializer : IMapInitializer
    {
        public void SourceToModel(IMap map)
        {
            Type modelType = map.Destination;
            Type otherType = map.Source;
            TypeInfo modelTypeInfo = modelType.GetTypeInfo();
            TypeInfo otherTypeInfo = otherType.GetTypeInfo();

            MapClassAttribute classAttribute = modelTypeInfo.GetCustomAttribute<MapClassAttribute>();
            if (classAttribute == null || classAttribute.OtherType != otherType)
                throw new InvalidOperationException("Model isn't property associated with the class");

            foreach (PropertyInfo property in modelTypeInfo.GetProperties())
            {
                if (property.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                    continue;

                MapPropertyAttribute attribute = property.GetCustomAttribute<MapPropertyAttribute>();
                if (attribute?.IgnoreToModel == false)
                {
                    string name = attribute.Name ?? property.Name;
                    PropertyInfo otherProperty = otherTypeInfo.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (otherProperty == null)
                        throw new InvalidOperationException($"The property {name} is not found in the referenced class");
                    if (otherProperty.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                        continue;
                    IMappingTarget target = new ClassPropertyAccessor(property);
                    IMappingSource source = new ClassPropertyAccessor(otherProperty);
                    var mapping = map.For(target);
                    mapping.Source = source;
                    mapping.MapFlag = attribute.MapFlags;
                }
            }
        }

        public void ModelToSource(IMap map)
        {
            Type modelType = map.Source;
            Type otherType = map.Destination;
            TypeInfo modelTypeInfo = modelType.GetTypeInfo();
            TypeInfo otherTypeInfo = otherType.GetTypeInfo();

            MapClassAttribute classAttribute = modelTypeInfo.GetCustomAttribute<MapClassAttribute>();
            if (classAttribute == null || classAttribute.OtherType != otherType)
                throw new InvalidOperationException("Model isn't properly associated with the class");

            foreach (PropertyInfo property in modelTypeInfo.GetProperties())
            {
                if (property.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                    continue;

                MapPropertyAttribute attribute = property.GetCustomAttribute<MapPropertyAttribute>();
                if (attribute?.IgnoreFromModel == false)
                {
                    string name = attribute.Name ?? property.Name;
                    PropertyInfo otherProperty = otherTypeInfo.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (otherProperty == null)
                        throw new InvalidOperationException($"The property {name} is not found in the referenced class");

                    if (otherProperty.GetCustomAttribute<DoNotAutoMapAttribute>() != null)
                        continue;

                    IMappingTarget target = new ClassPropertyAccessor(otherProperty);
                    IMappingSource source = new ClassPropertyAccessor(property);
                    var mapping = map.For(target);
                    mapping.Source = source;
                    mapping.MapFlag = attribute.MapFlags;
                }
            }
        }
    }
}

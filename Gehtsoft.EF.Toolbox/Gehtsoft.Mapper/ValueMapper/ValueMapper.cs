using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Gehtsoft.Tools2.Extensions;

namespace Gehtsoft.EF.Mapper
{
    [Flags]
    public enum MapFlag
    {
        None = 0x0000_0000,
        TrimStrings = 0x0000_0001,
        TrimToSeconds = 0x0000_0002,
        TrimToDate = 0x0000_0004,
    }

    public static class ValueMapper
    {
        private static readonly Type enumerableType = typeof(IEnumerable);
        private static readonly Type listType = typeof(IList);

        public static object MapValue(object sourceValue, Type destinationType, MapFlag flags = MapFlag.None, object destinationValue = null)
        {
            TypeInfo destinationTypeInfo = destinationType.GetTypeInfo();

            if (sourceValue == null)
            {
                if (destinationTypeInfo.IsValueType)
                    return Activator.CreateInstance(destinationType);
                else
                    return null;
            }

            Type destinationType1 = Nullable.GetUnderlyingType(destinationType);
            if (destinationType1 != null && destinationType1 != destinationType)
            {
                destinationType = destinationType1;
                destinationTypeInfo = destinationType.GetTypeInfo();
            }

            Type sourceType = sourceValue.GetType();

            if (sourceValue is string sv && ((flags & MapFlag.TrimStrings) == MapFlag.TrimStrings))
                sourceValue = sv.Trim();

            if ((sourceValue is DateTime time) && ((flags & MapFlag.TrimToDate) == MapFlag.TrimToDate))
                sourceValue = time.TruncateTime();

            if ((sourceValue is DateTime time1) && ((flags & MapFlag.TrimToSeconds) == MapFlag.TrimToSeconds))
                sourceValue = time1.TruncateToSeconds();

            if (sourceType == destinationType)
            {
                if (sourceType.IsValueType || sourceType.IsEnum || sourceType == typeof(string))
                    return sourceValue;

                IMap map = MapFactory.GetMap(sourceType, destinationType, false);
                if (map != null)
                {
                    if (destinationValue == null)
                    {
                        if (map.Factory != null)
                            destinationValue = map.Factory(sourceValue);
                        else
                            destinationValue = Activator.CreateInstance(destinationType);
                    }

                    map.Do(sourceValue, destinationValue);
                    return destinationValue;
                }

                return sourceValue;
            }

            TypeInfo sourceTypeInfo = sourceType.GetTypeInfo();

            if (destinationTypeInfo.IsValueType || destinationType == typeof(string))
            {
                if (destinationTypeInfo.IsEnum)
                {
                    if (sourceValue is string x)
                        return Enum.Parse(destinationType, x);
                    else
                        return Enum.ToObject(destinationType, sourceValue);
                }
                else
                    return Convert.ChangeType(sourceValue, destinationType, CultureInfo.InvariantCulture);
            }
            else
            {
                if (destinationType == typeof(object))
                    return sourceValue;

                if (destinationTypeInfo.IsArray && sourceTypeInfo.IsArray)
                {
                    Type destinationElementType = destinationTypeInfo.GetElementType();
                    Array sourceArray = (Array)sourceValue;
                    int length = sourceArray.Length;
                    Array destinationArray = Activator.CreateInstance(destinationType, new object[] { length }) as Array;

                    for (int i = 0; i < length; i++)
                        destinationArray.SetValue(MapValue(sourceArray.GetValue(i), destinationElementType), i);

                    return destinationArray;
                }
                else if (destinationTypeInfo.IsArray && enumerableType.IsAssignableFrom(sourceType))
                {
                    Type destinationElementType = destinationTypeInfo.GetElementType();
                    IEnumerable collection = (IEnumerable)sourceValue;
                    int length = 0;
                    foreach (var v in collection)
                        length++;
                    Array destinationArray = Activator.CreateInstance(destinationType, new object[] { length }) as Array;
                    int idx = 0;
                    foreach (object value in collection)
                        destinationArray.SetValue(MapValue(value, destinationElementType), idx++);
                    return destinationArray;
                }
                else if (listType.IsAssignableFrom(destinationType) && sourceTypeInfo.IsArray)
                {
                    Type destinationElementType = GetElementType(destinationType);
                    if (destinationElementType == null)
                        destinationElementType = typeof(object);
                    Array sourceArray = (Array)sourceValue;
                    int length = sourceArray.Length;
                    IList destinationCollection = Activator.CreateInstance(destinationType) as IList;
                    for (int i = 0; i < length; i++)
                        destinationCollection.Add(MapValue(sourceArray.GetValue(i), destinationElementType));
                    return destinationCollection;
                }
                else if (listType.IsAssignableFrom(destinationType) && enumerableType.IsAssignableFrom(sourceType))
                {
                    Type destinationElementType = GetElementType(destinationType);
                    IEnumerable collection = (IEnumerable)sourceValue;
                    IList destinationCollection = Activator.CreateInstance(destinationType) as IList;
                    foreach (object value in collection)
                        destinationCollection.Add(MapValue(value, destinationElementType));
                    return destinationCollection;
                }
                else
                {
                    IMap map = MapFactory.GetMap(sourceType, destinationType, false);
                    if (map == null)
                    {
                        if (destinationType.IsAssignableFrom(sourceType))
                            return sourceValue;

                        map = MapFactory.GetMap(sourceType, destinationType, true);
                        if (map == null)
                            throw new InvalidOperationException($"Map between {sourceType} and {destinationType} does not exists and cannot be automatically created");
                    }

                    if (destinationValue == null)
                    {
                        if (map.Factory != null)
                            destinationValue = map.Factory(sourceValue);
                        else
                            destinationValue = Activator.CreateInstance(destinationType);
                    }

                    map.Do(sourceValue, destinationValue);
                    return destinationValue;
                }
            }
        }

        public static Type GetElementType(Type type)
        {
            TypeInfo typeInfo = type.GetTypeInfo();
            if (typeInfo.IsArray)
                return typeInfo.GetElementType();

            foreach (Type iface in typeInfo.ImplementedInterfaces)
            {
                TypeInfo ifaceTypeInfo = iface.GetTypeInfo();
                if (ifaceTypeInfo.IsGenericType && ifaceTypeInfo.GenericTypeArguments?.Length == 1)
                {
                    Type candidateType = ifaceTypeInfo.GenericTypeArguments[0];
                    TypeInfo candidateTypeInfo = candidateType.GetTypeInfo();
                    if (candidateTypeInfo.IsValueType || candidateType == typeof(string) || (!candidateTypeInfo.IsAbstract && !candidateTypeInfo.IsInterface))
                    {
                        Type candidateInterface = typeof(ICollection<>).MakeGenericType(new Type[] { candidateType });
                        if (candidateInterface.GetTypeInfo().IsAssignableFrom(type))
                            return candidateType;

                        candidateInterface = typeof(IEnumerable<>).MakeGenericType(new Type[] { candidateType });
                        if (candidateInterface.GetTypeInfo().IsAssignableFrom(type))
                            return candidateType;
                    }
                }
            }
            return null;
        }
    }
}

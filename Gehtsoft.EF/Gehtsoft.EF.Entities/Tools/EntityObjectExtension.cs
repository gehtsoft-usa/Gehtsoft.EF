﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The extensions to get entity-associated data from the object.
    /// </summary>
    public static class EntityObjectExtension
    {
        /// <summary>
        /// The method checks whether the object is an entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsEfEntity(this object entity, Type type = null)
        {
            if (type == null && entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (type == null)
                type = entity.GetType();

            return type.GetCustomAttribute<EntityAttribute>() != null;
        }

        private static readonly ConcurrentDictionary<Type, PropertyInfo> gPrimaryKeys = new ConcurrentDictionary<Type, PropertyInfo>();

        /// <summary>
        /// Gets identifier of an entity object
        /// </summary>
        /// <typeparam name="T">The type of the identifier</typeparam>
        /// <param name="entity">The entity to get a value</param>
        /// <returns></returns>
        public static T GetEfEntityId<T>(this object entity) => (T)GetEfEntityId(entity, typeof(T), null);

        /// <summary>
        /// Gets the property information for the primary key property of the entity type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static PropertyInfo GetEfPrimaryKey(this Type type) => GetPrimaryKey(type);

        private static PropertyInfo GetPrimaryKey(Type entityType)
        {
            if (!gPrimaryKeys.TryGetValue(entityType, out PropertyInfo pk))
            {
                pk = null;
                foreach (PropertyInfo property in entityType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (property.GetCustomAttribute<AutoIdAttribute>() != null)
                    {
                        pk = property;
                        break;
                    }

                    EntityPropertyAttribute propertyAttribute = property.GetCustomAttribute<EntityPropertyAttribute>();
                    if (propertyAttribute != null && propertyAttribute.PrimaryKey)
                    {
                        pk = property;
                        break;
                    }
                }

                gPrimaryKeys[entityType] = pk ?? throw new ArgumentException($"Type {entityType.Name} is not an entity or does not have primary key defined");
            }
            return pk;
        }

        /// <summary>
        /// Gets the entity primary key value.
        /// </summary>
        /// <param name="entity">The entity object</param>
        /// <param name="desiredType">The desired primary key type</param>
        /// <param name="entityType"></param>
        /// <returns></returns>
        public static object GetEfEntityId(this object entity, Type desiredType = null, Type entityType = null)
        {
            if (entityType == null && entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (entityType == null)
                entityType = entity.GetType();

            PropertyInfo pk = GetPrimaryKey(entityType);

            if (desiredType == null)
                desiredType = pk.PropertyType;

            object value = pk.GetValue(entity);

            if (value == null)
            {
                if (desiredType.IsValueType)
                    return Activator.CreateInstance(desiredType);
                else
                    return null;
            }

            desiredType = Nullable.GetUnderlyingType(desiredType) ?? desiredType;

            if (desiredType.IsInstanceOfType(value))
                return value;

            if (desiredType.IsEnum)
                return Enum.ToObject(desiredType, value);
            else
                return Convert.ChangeType(value, desiredType);
        }
    }
}
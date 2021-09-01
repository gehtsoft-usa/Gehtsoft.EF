using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Utils
{
    /// <summary>
    /// The extension to access value from any object tree by path.
    ///
    /// The path is the sequence of the property names separated by commas (`.`).
    /// </summary>
    public static class EntityPathAccessor
    {
        private interface IAccessor
        {
            object GetValue(object thisObject);
        }

        private class PropertyAccessor : IAccessor
        {
            private readonly PropertyInfo mPropertyInfo;

            public object GetValue(object thisObject) => mPropertyInfo?.GetValue(thisObject);

            public PropertyAccessor(PropertyInfo propertyInfo)
            {
                mPropertyInfo = propertyInfo;
            }
        }

        private class ObjectAccessor : IAccessor
        {
            private readonly string mPropertyName;

            public object GetValue(object thisObject)
            {
                if (thisObject != null)
                {
                    var p = thisObject.GetType().GetProperty(mPropertyName);
                    if (p == null)
                    {
                        if (thisObject is IDictionary<string, object> dict)
                        {
                            if (dict.TryGetValue(mPropertyName, out var r))
                                return r;
                        }
                    }
                    else
                        return p.GetValue(thisObject);
                }
                return null;
            }

            public ObjectAccessor(string propertyInfo)
            {
                mPropertyName = propertyInfo;
            }
        }

        private static readonly Dictionary<Tuple<Type, string>, IAccessor[]> gPathDictionary = new Dictionary<Tuple<Type, string>, IAccessor[]>();
        private static readonly Type gDictType = typeof(IDictionary<string, object>);

        private static IAccessor[] ParsePath(Type baseType, string path)
        {
            string[] parts;

            if (path.Contains('.'))
                parts = path.Split('.');
            else
                parts = new string[] { path };

            Type currentType = baseType;
            IAccessor[] result = new IAccessor[parts.Length];

            int i = 0;
            foreach (string part in parts)
            {
                if (currentType == typeof(object) || gDictType.IsAssignableFrom(currentType))
                {
                    result[i] = new ObjectAccessor(part);
                    currentType = typeof(object);
                }
                else
                {
                    var propertyInfo = currentType.GetProperty(part);
                    if (propertyInfo == null)
                        throw new ArgumentException(nameof(path), $"Property {part} is not found in type {currentType.Name}");

                    result[i] = new PropertyAccessor(propertyInfo);
                    currentType = propertyInfo.PropertyType.GetTypeInfo();
                }
                i++;
            }

            return result;
        }

        private static IAccessor[] GetPath(Type baseType, string path)
        {
            Tuple<Type, string> key = new Tuple<Type, string>(baseType, path);
            if (gPathDictionary.TryGetValue(key, out var result))
                return result;
            result = ParsePath(baseType, path);
            gPathDictionary[key] = result;
            return result;
        }

        /// <summary>
        /// Gets value by the path
        /// </summary>
        /// <param name="entity">The root of the object tree</param>
        /// <param name="path">The path</param>
        /// <returns></returns>
        public static object ReadData(object entity, string path)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path.Length == 0)
                throw new ArgumentException("Path shall not be empty", nameof(path));

            var parsedPath = GetPath(entity.GetType(), path);
            foreach (var info in parsedPath)
                entity = entity == null ? null : info.GetValue(entity);

            return entity;
        }

        /// <summary>
        /// Gets value by the path (generic method)
        /// </summary>
        /// <typeparam name="TE">The type of the entity</typeparam>
        /// <typeparam name="TR">The type of the result value</typeparam>
        /// <param name="entity">The entity object</param>
        /// <param name="path">The path to the value</param>
        /// <returns></returns>
        public static TR ReadData<TE, TR>(TE entity, string path)
            => (TR)ReadData(entity, path);

        /// <summary>
        /// Parses and caches the path.
        ///
        /// This method is used to cache path explicitly. Otherwise,
        /// the path will be cached at the first use.
        /// </summary>
        /// <param name="entityType">The type of the entity</param>
        /// <param name="path">The path</param>
        public static void PreparePath(Type entityType, string path)
        {
            GetPath(entityType, path);
        }

        /// <summary>
        /// Checks whether the path is already cached.
        /// </summary>
        /// <param name="entityType"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool IsPathCached(Type entityType, string path)
            => gPathDictionary.ContainsKey(new Tuple<Type, string>(entityType, path));
    }
}

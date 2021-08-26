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
        private static readonly Dictionary<Tuple<Type, string>, PropertyInfo[]> mPathDictionary = new Dictionary<Tuple<Type, string>, PropertyInfo[]>();

        private static PropertyInfo[] ParsePath(Type baseType, string path)
        {
            string[] parts;

            if (path.Contains('.'))
                parts = path.Split('.');
            else
                parts = new string[] { path };

            TypeInfo currInfo = baseType.GetTypeInfo();
            PropertyInfo[] result = new PropertyInfo[parts.Length];

            int i = 0;
            foreach (string part in parts)
            {
                PropertyInfo info = currInfo.GetProperty(part);
                result[i] = info ?? throw new ArgumentException(nameof(path), $"Property {part} is not found in type {currInfo.Name}");
                currInfo = info.PropertyType.GetTypeInfo();
                i++;
            }

            return result;
        }

        private static PropertyInfo[] GetPath(Type baseType, string path)
        {
            Tuple<Type, string> key = new Tuple<Type, string>(baseType, path);
            if (mPathDictionary.TryGetValue(key, out PropertyInfo[] result))
                return result;
            result = ParsePath(baseType, path);
            mPathDictionary[key] = result;
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

            PropertyInfo[] parsedPath = GetPath(entity.GetType(), path);
            foreach (PropertyInfo info in parsedPath)
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
            => mPathDictionary.ContainsKey(new Tuple<Type, string>(entityType, path));
    }
}

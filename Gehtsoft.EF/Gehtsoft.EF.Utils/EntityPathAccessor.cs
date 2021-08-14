using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Utils
{
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

        public static TR ReadData<TE, TR>(TE entity, string path)
            => (TR)ReadData(entity, path);

        public static void PreparePath(Type entityType, string path)
        {
            GetPath(entityType, path);
        }

        public static bool IsPathCached(Type entityType, string path)
            => mPathDictionary.ContainsKey(new Tuple<Type, string>(entityType, path));
    }
}

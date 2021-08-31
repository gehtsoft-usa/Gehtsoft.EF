using System;
using System.Data;
using System.Reflection;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The helper to compare object tree of two entities.
    /// </summary>
    public static class EntityComparerHelper
    {
        /// <summary>
        /// Compares to objects.
        /// </summary>
        /// <param name="objectA"></param>
        /// <param name="objectB"></param>
        /// <returns></returns>
        public new static bool Equals(object objectA, object objectB)
        {
            if (objectA == null && objectB == null)
                return true;
            if (objectA == null || objectB == null)
                return false;
            if (ReferenceEquals(objectA, objectB))
                return true;

            if (objectA.GetType() == objectB.GetType())
            {
                Type t = objectA.GetType();
                PropertyInfo[] properties = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (PropertyInfo property in properties)
                {
                    EntityPropertyAttribute attribute = property.GetCustomAttribute<EntityPropertyAttribute>();

                    if (attribute != null)
                    {
                        bool rc;
                        Type p = property.PropertyType;
                        Attribute attribute1 = p.GetTypeInfo().GetCustomAttribute(typeof(EntityAttribute));
                        if (attribute1 != null)
                        {
                            rc = EntityComparerHelper.Equals(property.GetValue(objectA), property.GetValue(objectB));
                            if (!rc)
                                return false;
                        }
                        else
                        {
                            object objectAA, objectBB;
                            objectAA = property.GetValue(objectA);
                            objectBB = property.GetValue(objectB);

#pragma warning disable IDE0038 // Use pattern matching
                            if (attribute.DbType == DbType.Date && objectAA is DateTime && objectBB is DateTime)
                            {
                                DateTime a = (DateTime)objectAA;
                                DateTime b = (DateTime)objectBB;
                                if (a.Year != b.Year || a.Month != b.Month || a.Day != b.Day)
                                    return false;
                            }
                            else if (attribute.DbType == DbType.DateTime && objectAA is DateTime && objectBB is DateTime)
                            {
                                DateTime a = (DateTime)objectAA;
                                DateTime b = (DateTime)objectBB;
                                if (a.Year != b.Year || a.Month != b.Month || a.Day != b.Day || a.Hour != b.Hour || a.Minute != b.Minute || a.Second != b.Second)
                                    return false;
                            }
                            else if (attribute.DbType == DbType.Double && attribute.Precision > 0 && objectAA is double && objectBB is double)
                            {
                                double a = (double)objectAA;
                                double b = (double)objectBB;
                                if (Math.Abs(a - b) > Math.Pow(10, -attribute.Precision))
                                    return false;
                            }
                            else if ((attribute.DbType == DbType.Binary || attribute.DbType == DbType.Object) &&
                                     objectAA is byte[] && objectBB is byte[])
                            {
                                byte[] a = (byte[])objectAA;
                                byte[] b = (byte[])objectBB;

                                if (a.Length != b.Length)
                                    return false;
                                for (int i = 0; i < a.Length; i++)
                                    if (a[i] != b[i])
                                        return false;
                            }
                            else if (!object.Equals(objectAA, objectBB))
                                return false;
#pragma warning restore IDE0038 // Use pattern matching
                        }
                    }
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Gets hash code of an entity.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static int GetHashCode(object obj)
        {
            if (obj == null)
                return 0.GetHashCode();
            var t = obj.GetType();

            int hash = -1923861349;
            PropertyInfo[] properties = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                var attribute = property.GetCustomAttribute<EntityPropertyAttribute>();
                if (attribute != null)
                {
                    int valueHash = 0;

                    var value = property.GetValue(obj);
                    if (value == null)
                        valueHash = 0.GetHashCode();
                    else
                    {
                        var propertyType = property.PropertyType;
                        var entityAttribute = propertyType.GetCustomAttribute<EntityAttribute>();
                        if (entityAttribute != null)
                            valueHash = GetHashCode(value);
                        else if (value is byte[] b)
                        {
                            int hash1 =  -1923861349;
                            for (int i = 0; i < b.Length; i++)
                            {
                                unchecked { hash1 = hash1 * -1521134295 + b[i].GetHashCode(); }
                            }
                            valueHash = hash1;
                        }
                        else
                            valueHash = value.GetHashCode();
                    }

                    unchecked { hash = hash * -1521134295 + valueHash; }
                }
            }
            return hash;
        }
    }
}

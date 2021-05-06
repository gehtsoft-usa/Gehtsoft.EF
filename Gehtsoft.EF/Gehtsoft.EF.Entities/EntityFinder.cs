using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities
{
    public static class EntityFinder
    {
#pragma warning disable S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"
        public class EntityTypeInfo : IComparable<EntityTypeInfo>
#pragma warning restore S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"
        {
            public string Scope { get; set; }
            public Type EntityType { get; set; }
            public bool Obsolete { get; set; }
            public string Table { get; set; }
            public EntityNamingPolicy NamingPolicy { get; set; }
            public List<Type> DependsOn { get; } = new List<Type>();
            public List<EntityTypeInfo> DependsOnInfo { get; } = new List<EntityTypeInfo>();
            public bool View { get; set; }
            public Type Metadata { get; set; }

            public int CompareTo(object obj)
            {
                EntityTypeInfo other = obj as EntityTypeInfo;
                return CompareTo(other);
            }

            public int CompareTo(EntityTypeInfo other)
            {
                if (other == null)
                    return 1;

                if (View && !other.View)
                    return 1;
                if (!View && other.View)
                    return -1;

                if (View && other.View)
                    return CompareNames(other);

                if (DoesDependOn(other))
                    return 1;
                if (other.DoesDependOn(this))
                    return -1;

                return CompareNames(other);
            }

            private int CompareNames(EntityTypeInfo other) => string.Compare(Table ?? EntityType.Name, other.Table ?? EntityType.Name, StringComparison.OrdinalIgnoreCase);

            public bool DoesDependOn(EntityTypeInfo info)
            {
                foreach (EntityTypeInfo dep in DependsOnInfo)
                {
                    if (dep == info || dep.DoesDependOn(info))
                        return true;
                }
                return false;
            }

            public override string ToString()
            {
                return $"{Table ?? EntityType.Name}({EntityType.Name})";
            }
        }

        public static EntityTypeInfo[] FindEntities(IEnumerable<Assembly> assemblies, string scope, bool includeObsolete)
        {
            List<EntityTypeInfo> types = new List<EntityTypeInfo>();
            Dictionary<Type, EntityTypeInfo> dict = new Dictionary<Type, EntityTypeInfo>();

            foreach (Assembly asm in assemblies)
            {
                foreach (Type type in asm.GetTypes())
                {
                    EntityAttribute entityAttribute;
                    ObsoleteEntityAttribute obsoleteEntityAttribute;

                    entityAttribute = type.GetTypeInfo().GetCustomAttribute<EntityAttribute>();
                    if (entityAttribute != null && (scope == null || scope == entityAttribute.Scope))
                    {
                        EntityTypeInfo eti = new EntityTypeInfo()
                        {
                            EntityType = type,
                            Table = entityAttribute.Table,
                            Scope = entityAttribute.Scope,
                            NamingPolicy = entityAttribute.NamingPolicy,
                            Obsolete = false,
                            View = entityAttribute.View,
                            Metadata = entityAttribute.Metadata,
                        };

                        types.Add(eti);
                        dict[type] = eti;
                        FindDependencies(eti);
                    }
                    else
                    {
                        if (includeObsolete)
                        {
                            obsoleteEntityAttribute = type.GetTypeInfo().GetCustomAttribute<ObsoleteEntityAttribute>();
                            if (obsoleteEntityAttribute != null && (scope == null || obsoleteEntityAttribute.Scope == scope))
                            {
                                EntityTypeInfo eti = new EntityTypeInfo
                                {
                                    EntityType = type,
                                    Scope = obsoleteEntityAttribute.Scope,
                                    Table = obsoleteEntityAttribute.Table,
                                    NamingPolicy = obsoleteEntityAttribute.NamingPolicy,
                                    Obsolete = true,
                                    View = obsoleteEntityAttribute.View,
                                    Metadata = obsoleteEntityAttribute.Metadata,
                                };
                                FindDependencies(eti);
                                types.Add(eti);
                                dict[type] = eti;
                            }
                        }
                    }
                }
            }
            //resolve dependency types to info
            foreach (EntityTypeInfo eti in types)
            {
                foreach (Type type in eti.DependsOn)
                {
                    EntityTypeInfo eti1 = dict[type];
                    if (eti1 != eti)
                        eti.DependsOnInfo.Add(eti1);
                }
            }
            return types.ToArray();
        }

        private static void FindDependencies(EntityTypeInfo eti)
        {
            foreach (PropertyInfo property in eti.EntityType.GetTypeInfo().GetProperties())
            {
                EntityPropertyAttribute propertyAttribute = property.GetCustomAttribute<EntityPropertyAttribute>();
                if (propertyAttribute != null && propertyAttribute.ForeignKey)
                    eti.DependsOn.Add(property.PropertyType);

                ObsoleteEntityPropertyAttribute obsoletePropertyAttribute = property.GetCustomAttribute<ObsoleteEntityPropertyAttribute>();
                if (obsoletePropertyAttribute != null && obsoletePropertyAttribute.ForeignKey)
                    eti.DependsOn.Add(property.PropertyType);
            }
        }

        private static void InsertIntoList(List<EntityTypeInfo> info, EntityTypeInfo entity)
        {
            foreach (EntityTypeInfo entity1 in info)
                if (entity1 == entity)
                    return;
            foreach (EntityTypeInfo dep in entity.DependsOnInfo)
                InsertIntoList(info, dep);
            info.Add(entity);
        }

        public static void ArrageEntities(EntityTypeInfo[] entities)
        {
            List<EntityTypeInfo> output = new List<EntityTypeInfo>();
            foreach (EntityTypeInfo info in entities.Where(e => !e.View))
                InsertIntoList(output, info);
            foreach (EntityTypeInfo info in entities.Where(e => e.View))
                InsertIntoList(output, info);
            if (output.Count != entities.Length)
                throw new InvalidOperationException();
            for (int i = 0; i < output.Count; i++)
                entities[i] = output[i];
        }
    }
}
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

#pragma warning disable S1210 // "Equals" and the comparison operators should be overridden when implementing "IComparable"

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// The class to find all entity types.
    /// </summary>
    public static class EntityFinder
    {
        /// <summary>
        /// The information about the entity type
        /// </summary>
        public class EntityTypeInfo : IComparable<EntityTypeInfo>
        {
            /// <summary>
            /// The entity type
            /// </summary>
            public string Scope { get; set; }
            /// <summary>
            /// Runtime type of the entity
            /// </summary>
            public Type EntityType { get; set; }
            /// <summary>
            /// The flag indicating whether the entity is obsolete
            /// </summary>
            public bool Obsolete { get; set; }
            /// <summary>
            /// The name of the table
            /// </summary>
            public string Table { get; set; }
            /// <summary>
            /// The naming policy associated with the table
            /// </summary>
            public EntityNamingPolicy NamingPolicy { get; set; }
            /// <summary>
            /// The list of the entity on which this entity depends
            /// </summary>
            public List<Type> DependsOn { get; } = new List<Type>();
            /// <summary>
            /// The list of the entity information of the entities on which this entity depends
            /// </summary>
            public List<EntityTypeInfo> DependsOnInfo { get; } = new List<EntityTypeInfo>();
            /// <summary>
            /// The flag indicating that the entity is associated with a view.
            /// </summary>
            public bool View { get; set; }

            /// <summary>
            /// The type of the entity metadata object.
            ///
            /// The metadata object is used to provide additional
            /// information about the entity. To provide that information
            /// the metadata should implement appropriate interface,
            /// for example
            /// [clink=Gehtsoft.EF.Db.SqlDb.Metadata.ICompositeIndexMetadata]ICompositeIndexMetadata[/clink] or
            /// [clink=Gehtsoft.EF.Db.SqlDb.Metadata.IViewCreationMetadata]IViewCreationMetadata[/clink].
            /// </summary>
            public Type Metadata { get; set; }

            /// <summary>
            /// Compares the entity information to another object.
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            [DocgenIgnore]
            public int CompareTo(object obj)
            {
                EntityTypeInfo other = obj as EntityTypeInfo;
                return CompareTo(other);
            }

            /// <summary>
            /// Compares the entity information to another entity information.
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            [DocgenIgnore]
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

            /// <summary>
            /// Checks whether this entity depends on the entity specified.
            /// </summary>
            /// <param name="info"></param>
            /// <returns></returns>
            public bool DoesDependOn(EntityTypeInfo info)
            {
                foreach (EntityTypeInfo dep in DependsOnInfo)
                {
                    if (dep == info || dep.DoesDependOn(info))
                        return true;
                }
                return false;
            }

            /// <summary>
            /// Returns the entity information title.
            /// </summary>
            /// <returns></returns>
            [DocgenIgnore]
            public override string ToString()
            {
                return $"{Table ?? EntityType.Name}({EntityType.Name})";
            }
        }

        /// <summary>
        /// Finds entities.
        /// </summary>
        /// <param name="assemblies">The list of the assemblies to search in</param>
        /// <param name="scope">The scope to search or `null` to search in all scopes</param>
        /// <param name="includeObsolete">The flag indicating whether the obsolete entities shall also be included</param>
        /// <returns></returns>
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

        /// <summary>
        /// Arranges entities according their dependency tree.
        ///
        /// After entities are properly arranged, you can use their
        /// direct order to create DB objects and their reverse order
        /// to drop them.
        /// </summary>
        /// <param name="entities"></param>
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
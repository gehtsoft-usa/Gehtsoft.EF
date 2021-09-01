using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class DynamicEntityDiscoverer : ColumnDisoverer, IEntityDisoverer, IEntityProbe
    {
        private static readonly Type DynamicEntityType = typeof(DynamicEntity);

        public TableDescriptor Discover(AllEntities entities, Type type)
        {
            EntityAttribute typeAttribute;
            ObsoleteEntityAttribute obsoleteTypeAttribute = null;
            DynamicEntity dynamicEntity;
            TableDescriptor descriptor;
            
            if (DynamicEntityType.IsAssignableFrom(type))
            {
                dynamicEntity = (DynamicEntity)Activator.CreateInstance(type);
                typeAttribute = dynamicEntity.EntityAttribute;
                if (typeAttribute == null)
                    obsoleteTypeAttribute = dynamicEntity.ObsoleteEntityAttribute;
            }
            else
                return null;
            
            EntityNamingPolicy namingPolicy = EntityNamingPolicy.Default;

            string tableScope;
            if (typeAttribute == null)
            {
                if (obsoleteTypeAttribute == null)
                      return null;

                tableScope = obsoleteTypeAttribute.Scope;
                descriptor = new TableDescriptor(obsoleteTypeAttribute.Table) { Obsolete = true, View = obsoleteTypeAttribute.View };
                if (obsoleteTypeAttribute.Metadata != null)
                    descriptor.Metadata = Activator.CreateInstance(obsoleteTypeAttribute.Metadata);
            }
            else
            {
                tableScope = typeAttribute.Scope;
                namingPolicy = typeAttribute.NamingPolicy;
                descriptor = new TableDescriptor(typeAttribute.Table) { View = typeAttribute.View };
                if (typeAttribute.Metadata != null)
                    descriptor.Metadata = Activator.CreateInstance(typeAttribute.Metadata);
            }

            namingPolicy = (namingPolicy == EntityNamingPolicy.Default ? entities.NamingPolicy[tableScope] : namingPolicy);

            if (descriptor.Name == null)
                descriptor.Name = EntityNameConvertor.ConvertTableName(type.Name, namingPolicy == EntityNamingPolicy.BackwardCompatibility ? EntityNamingPolicy.AsIs : namingPolicy);

            descriptor.Scope = tableScope;

            foreach (IDynamicEntityProperty property in dynamicEntity.Properties)
                CreateColumnDescriptor(type, entities, namingPolicy, descriptor, new DynamicPropertyAccessor(property));
            
            return descriptor;
        }

        public EntityFinder.EntityTypeInfo ProbeClass(Type type, string scope, bool includeObsolete)
        {
            EntityAttribute entityAttribute;
            ObsoleteEntityAttribute obsoleteEntityAttribute;

            if (!DynamicEntityType.IsAssignableFrom(type))
                return null;

            var dynamicEntity = (DynamicEntity)Activator.CreateInstance(type);
            entityAttribute = dynamicEntity.EntityAttribute;

            if (entityAttribute != null)
            {
                if (!string.IsNullOrEmpty(scope) && entityAttribute.Scope != scope)
                    return null;

                var eti = new EntityFinder.EntityTypeInfo()
                {
                    EntityType = type,
                    Table = entityAttribute.Table,
                    Scope = entityAttribute.Scope,
                    NamingPolicy = entityAttribute.NamingPolicy,
                    Obsolete = false,
                    View = entityAttribute.View,
                    Metadata = entityAttribute.Metadata,
                };
                FindDependencies(dynamicEntity, eti, includeObsolete);
                return eti;
            }
            else
            {
                if (includeObsolete)
                {
                    obsoleteEntityAttribute = dynamicEntity.ObsoleteEntityAttribute;
                    if (obsoleteEntityAttribute != null)
                    {
                        if (!string.IsNullOrEmpty(scope) && obsoleteEntityAttribute.Scope != scope)
                            return null;

                        var eti = new EntityFinder.EntityTypeInfo
                        {
                            EntityType = type,
                            Scope = obsoleteEntityAttribute.Scope,
                            Table = obsoleteEntityAttribute.Table,
                            NamingPolicy = obsoleteEntityAttribute.NamingPolicy,
                            Obsolete = true,
                            View = obsoleteEntityAttribute.View,
                            Metadata = obsoleteEntityAttribute.Metadata,
                        };
                        FindDependencies(dynamicEntity, eti, includeObsolete);
                        return eti;
                    }
                }
            }
            return null;
        }

        private static void FindDependencies(DynamicEntity dynamicEntity, EntityFinder.EntityTypeInfo eti, bool includeObsolete)
        {
            foreach (var property in dynamicEntity.Properties)
            {
                EntityPropertyAttribute propertyAttribute = property.EntityPropertyAttribute;
                if (propertyAttribute != null && propertyAttribute.ForeignKey)
                    eti.DependsOn.Add(property.PropertyType);

                if (includeObsolete)
                {
                    ObsoleteEntityPropertyAttribute obsoletePropertyAttribute = property.ObsoleteEntityPropertyAttribute;
                    if (obsoletePropertyAttribute != null && obsoletePropertyAttribute.ForeignKey)
                        eti.DependsOn.Add(property.PropertyType);
                }
            }
        }
    }
}

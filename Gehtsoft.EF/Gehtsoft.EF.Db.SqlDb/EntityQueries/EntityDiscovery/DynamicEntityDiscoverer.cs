using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class DynamicEntityDiscoverer : ColumnDisoverer, IEntityDisoverer
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
    }
}

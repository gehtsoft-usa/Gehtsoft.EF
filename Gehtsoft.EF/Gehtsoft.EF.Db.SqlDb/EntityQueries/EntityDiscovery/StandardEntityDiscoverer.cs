using System;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class StandardEntityDiscoverer : ColumnDiscoverer, IEntityDisoverer
    {
        public TableDescriptor Discover(AllEntities entities, Type type)
        {
            EntityAttribute typeAttribute;
            ObsoleteEntityAttribute obsoleteTypeAttribute = null;
            TableDescriptor descriptor;

            typeAttribute = type.GetCustomAttribute<EntityAttribute>();
            if (typeAttribute == null)
                obsoleteTypeAttribute = type.GetCustomAttribute<ObsoleteEntityAttribute>();

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

            foreach (PropertyInfo propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                CreateColumnDescriptor(type, entities, namingPolicy, descriptor, new PropertyAccessor(propertyInfo));

            return descriptor;
        }
    }
}

using System;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class DynamicEntityProperty : IDynamicEntityProperty
    {
        public Type PropertyType { get; set; }
        public string Name { get; set; }
        public EntityPropertyAttribute EntityPropertyAttribute { get; set; }
        public ObsoleteEntityPropertyAttribute ObsoleteEntityPropertyAttribute { get; set; }

        public DynamicEntityProperty()
        {

        }

        public DynamicEntityProperty(Type propertyType, string name, EntityPropertyAttribute attribute)
        {
            PropertyType = propertyType;
            Name = name;
            EntityPropertyAttribute = attribute;
        }
    }
}
using System;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The default implementation of the dynamic entity property information interface
    /// </summary>
    public class DynamicEntityProperty : IDynamicEntityProperty
    {
        [DocgenIgnore]
        public Type PropertyType { get; set; }
        [DocgenIgnore]
        public string Name { get; set; }
        [DocgenIgnore]
        public EntityPropertyAttribute EntityPropertyAttribute { get; set; }
        [DocgenIgnore]
        public ObsoleteEntityPropertyAttribute ObsoleteEntityPropertyAttribute { get; set; }

        [DocgenIgnore]
        public DynamicEntityProperty()
        {
        }
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="propertyType"></param>
        /// <param name="name"></param>
        /// <param name="attribute"></param>
        public DynamicEntityProperty(Type propertyType, string name, EntityPropertyAttribute attribute)
        {
            PropertyType = propertyType;
            Name = name;
            EntityPropertyAttribute = attribute;
        }
    }
}
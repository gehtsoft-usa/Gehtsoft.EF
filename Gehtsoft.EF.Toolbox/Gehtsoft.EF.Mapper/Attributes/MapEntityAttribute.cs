using System;

namespace Gehtsoft.EF.Mapper
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MapEntityAttribute : MapSpecificationAttribute
    {
        public Type EntityType { get; set; }

        public MapEntityAttribute() : base(typeof(EntityMapInitializer))
        {

        }
        
        public MapEntityAttribute(Type entityType) : this()
        {
            EntityType = entityType;
        }
    }
}

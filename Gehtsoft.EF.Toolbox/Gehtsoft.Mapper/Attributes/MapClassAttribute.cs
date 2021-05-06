using System;
using Gehtsoft.EF.Mapper;

namespace Gehtsoft.EF.Mapper
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MapClassAttribute : MapSpecificationAttribute
    {
        public Type OtherType { get; set; }

        public MapClassAttribute() : base(typeof(ClassToModelInitializer))
        {
        }

        public MapClassAttribute(Type otherType) : this()
        {
            OtherType = otherType;
        }
    }
}
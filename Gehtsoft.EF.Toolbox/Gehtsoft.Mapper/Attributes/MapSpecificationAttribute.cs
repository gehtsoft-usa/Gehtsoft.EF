using System;

namespace Gehtsoft.EF.Mapper
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MapSpecificationAttribute : Attribute
    {
        private Type InitializerType { get; set; }

        protected MapSpecificationAttribute(Type initializerType)
        {
            InitializerType = initializerType;
        }
        
        internal IMapInitializer GetInitializer()
        {
            IMapInitializer initializer = Activator.CreateInstance(InitializerType) as IMapInitializer;
            if (initializer == null)
                throw new InvalidOperationException($"The type {InitializerType} does not implement {nameof(IMapInitializer)}");
            return initializer;
        }
    }
}
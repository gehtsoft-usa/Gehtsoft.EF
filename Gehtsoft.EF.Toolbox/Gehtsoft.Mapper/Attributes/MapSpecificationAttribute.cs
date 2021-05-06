using System;

namespace Gehtsoft.EF.Mapper
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MapSpecificationAttribute : Attribute
    {
        private Type InitializerType { get; }

        protected MapSpecificationAttribute(Type initializerType)
        {
            InitializerType = initializerType;
        }

        internal IMapInitializer GetInitializer()
        {
            if (!(Activator.CreateInstance(InitializerType) is IMapInitializer initializer))
                throw new InvalidOperationException($"The type {InitializerType} does not implement {nameof(IMapInitializer)}");
            return initializer;
        }
    }
}
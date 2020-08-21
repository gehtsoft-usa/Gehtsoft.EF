using System;

namespace Gehtsoft.EF.Mapper
{
    public interface IMappingTarget : IEquatable<IMappingTarget>
    {
        string Name { get; }
        Type ValueType { get; }
        void Set(object obj, object value);
    }
}
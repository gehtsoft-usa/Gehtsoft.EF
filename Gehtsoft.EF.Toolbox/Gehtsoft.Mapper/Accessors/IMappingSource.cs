using System;

namespace Gehtsoft.EF.Mapper
{
    public interface IMappingSource
    {
        string Name { get; }
        Type ValueType { get; }
        object Get(object obj);
    }
}
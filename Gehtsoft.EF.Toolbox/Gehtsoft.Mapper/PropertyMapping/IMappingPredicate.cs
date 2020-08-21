using System;

namespace Gehtsoft.EF.Mapper
{
    public interface IMappingPredicate
    {
        Type ParameterType { get; }
        bool Evaluate(object obj);
    }
}
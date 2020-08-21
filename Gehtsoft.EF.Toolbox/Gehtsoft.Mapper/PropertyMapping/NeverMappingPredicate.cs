using System;

namespace Gehtsoft.EF.Mapper
{
    public class NeverMappingPredicate : IMappingPredicate
    {
        public Type ParameterType => null;
        public bool Evaluate(object obj) => false;
    }
}
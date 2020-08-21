using System.Collections.Generic;

namespace Gehtsoft.EF.Mapper
{
    public interface IPropertyMappingCollection : IEnumerable<IPropertyMapping>
    {
        void Add(IPropertyMapping mapping);
        int Count { get; }
        IPropertyMapping this[int index] { get; }
        int Find(IMappingTarget target);
    }
}
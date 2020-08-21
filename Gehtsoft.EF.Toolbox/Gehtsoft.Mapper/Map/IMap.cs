using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Mapper
{
    public interface IMap : IEquatable<IMap>, IEnumerable<IPropertyMapping>
    {
        Type Source { get; }
        Type Destination { get; }
        IPropertyMappingCollection Mappings { get; }
        IMappingActionCollection Pre { get; }
        IMappingActionCollection Post { get; }
        Func<object, object> Factory { get; }

        IPropertyMapping For(IMappingTarget target);
        void Do(object from, object to);
        void Do(object from, object to, bool ignoreNull);
        object Do(object from);
    }
}
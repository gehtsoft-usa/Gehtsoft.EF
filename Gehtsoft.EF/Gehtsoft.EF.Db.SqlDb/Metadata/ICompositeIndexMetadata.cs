using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    public interface ICompositeIndexMetadata
    {
        IEnumerable<CompositeIndex> Indexes { get; }
    }
}

using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    /// <summary>
    /// Provides the information about composite (e.g. multi-column) indexes 
    /// associated with an entity or a table.
    /// </summary>
    public interface ICompositeIndexMetadata
    {
        /// <summary>
        /// Returns the enumeration of the indexes.
        /// </summary>
        IEnumerable<CompositeIndex> Indexes { get; }
    }
}

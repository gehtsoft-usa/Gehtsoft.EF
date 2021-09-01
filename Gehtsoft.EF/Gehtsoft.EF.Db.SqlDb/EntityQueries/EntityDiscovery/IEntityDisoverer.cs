using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The interface for entity discoverer.
    ///
    /// Implement this interface and add it using <see cref="AllEntities.AddDiscoverer(IEntityDisoverer)"/>
    /// to enable another way to discover entities.
    ///
    /// If you implement this interface, implement also <see cref="Gehtsoft.EF.Entities.IEntityProbe"/> interface
    /// to enable finding the entities by <see cref="Gehtsoft.EF.Entities.EntityFinder"/>.
    /// </summary>
    public interface IEntityDisoverer
    {
        TableDescriptor Discover(AllEntities entities, Type type);
    }
}

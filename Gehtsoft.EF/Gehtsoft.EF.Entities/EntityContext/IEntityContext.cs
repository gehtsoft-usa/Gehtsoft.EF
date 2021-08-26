using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The interface to the context of the entities.
    ///
    /// You can use this interface as a simplified way
    /// to access entities. This interface is independent
    /// from the specifics of the entity storage
    /// implementation (e.g. SQL or NoSQL).
    /// </summary>
    public interface IEntityContext : IDisposable
    {
        /// <summary>
        /// Returns the list of the tables.
        /// </summary>
        /// <returns></returns>
        IEntityTable[] ExistingTables();

        /// <summary>
        /// Returns a query to drop the table associated with the entity.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IEntityQuery DropEntity(Type type);

        /// <summary>
        /// Returns a query to create a table associated with the entity.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IEntityQuery CreateEntity(Type type);

        /// <summary>
        /// Returns a query to insert an entity to the storage.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="createKey">The flag indicating whether automatic (e.g. auto-insert) keys should be automatically created.</param>
        /// <returns></returns>
        IModifyEntityQuery InsertEntity(Type type, bool createKey);

        /// <summary>
        /// Returns a query to update the entity in the storage.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IModifyEntityQuery UpdateEntity(Type type);

        /// <summary>
        /// Returns a query to delete one entity from the storage.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IModifyEntityQuery DeleteEntity(Type type);

        /// <summary>
        /// Returns a query to delete multiple entities from the storage.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IContextQueryWithCondition DeleteMultiple(Type type);

        /// <summary>
        /// Returns a query to select entities from the storage.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IContextSelect Select(Type type);

        /// <summary>
        /// Returns a query to count the number of the entities in the storage.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IContextCount Count(Type type);

        /// <summary>
        /// Begins the transaction operation.
        /// </summary>
        /// <returns></returns>
        IEntityContextTransaction BeginTransaction();
    }
}
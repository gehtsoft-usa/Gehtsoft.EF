using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The serialization callback interface.
    ///
    /// If the entity implements this interface, this method is called every time
    /// before the entity is saved to the database or after the entity is restored.
    /// </summary>
    public interface IEntitySerializationCallback
    {
        /// <summary>
        /// The entity is about to be saved.
        /// </summary>
        /// <param name="connection"></param>
        void BeforeSerialization(SqlDbConnection connection);
        /// <summary>
        /// The entity has just been restored.
        /// </summary>
        /// <param name="query"></param>
        void AfterDeserealization(SelectEntitiesQueryBase query);
    }
}

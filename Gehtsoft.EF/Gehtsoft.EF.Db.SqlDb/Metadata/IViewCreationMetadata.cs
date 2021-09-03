using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    /// <summary>
    /// Provides the information for automatic creation of a view
    /// </summary>
    public interface IViewCreationMetadata
    {
        /// <summary>
        /// Returns select query builder.
        ///
        /// Note: you can use
        /// <see cref="Gehtsoft.EF.Db.SqlDb.EntityQueries.SelectEntitiesQueryBase.SelectBuilder">SelectEntitiesQueryBase.SelectBuilder</see>
        /// to create a select query from an entity query.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        SelectQueryBuilder GetSelectQuery(SqlDbConnection connection);
    }
}

using System;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The base class for all query builders.
    /// </summary>
    public abstract class AQueryBuilder
    {
        protected SqlDbLanguageSpecifics mSpecifics;

        protected AQueryBuilder(SqlDbLanguageSpecifics specifics)
        {
            mSpecifics = specifics;
        }

        /// <summary>
        /// Prepares the query.
        /// </summary>
        public abstract void PrepareQuery();

        /// <summary>
        /// Returns the query in SQL.
        ///
        /// You must call <see cref="PrepareQuery"/> before getting the query.
        /// </summary>
        public abstract string Query { get; }
    }
}

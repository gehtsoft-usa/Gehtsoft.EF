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

        /// <summary>
        /// Gets a reference to a column of the associated entity.
        /// 
        /// The reference is used when sub-query condition must have a reference to the main query.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public virtual IInQueryFieldReference GetReference(TableDescriptor.ColumnInfo column) => throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
    }
}

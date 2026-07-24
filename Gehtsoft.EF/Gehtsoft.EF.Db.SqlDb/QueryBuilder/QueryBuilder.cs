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
        /// When `true`, the builder skips the string-scalar (SQL-injection) guard on the raw
        /// expressions added to it - column expressions, resultset expressions, and raw condition
        /// operands. Set it only when every raw expression fed to this builder is
        /// framework-generated and trusted (for example a dialect's <see cref="SqlFunctionId.Now"/>
        /// rendering, which legitimately contains a quoted literal), never for a builder that carries
        /// caller-supplied text. The default is `false`.
        /// </summary>
        public bool SuppressScalarProtection { get; set; }

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

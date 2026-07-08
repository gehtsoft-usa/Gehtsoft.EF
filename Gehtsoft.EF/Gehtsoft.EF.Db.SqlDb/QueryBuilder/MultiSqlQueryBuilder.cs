using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// Combines several sub-queries into a single command so they run in one execution.
    ///
    /// The statements are wrapped using the driver's block markers
    /// (<see cref="SqlDbLanguageSpecifics.PreBlock"/> / <see cref="SqlDbLanguageSpecifics.PostBlock"/>)
    /// - e.g. an engine that requires a `BEGIN ... END;` block gets one, while engines that accept
    /// plain `;`-separated statements get those - and each statement is terminated with the driver's
    /// <see cref="SqlDbLanguageSpecifics.StatementTerminator"/> (a semicolon, which is the statement
    /// terminator on every supported engine, including inside a PL/SQL block).
    ///
    /// This builder does NOT manage parameters: parameter names come from the sub-queries as-is, so
    /// repeated names across sub-queries refer to a single parameter (bound once). Ensuring names
    /// are unique where the values differ - and shared where they are the same - is the caller's job.
    /// </summary>
    public class MultiSqlQueryBuilder : AQueryBuilder
    {
        private readonly List<AQueryBuilder> mQueries = new List<AQueryBuilder>();
        private string mQuery;

        [DocgenIgnore]
        public override string Query => mQuery;

        [DocgenIgnore]
        internal protected MultiSqlQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        /// <summary>
        /// Adds a sub-query to be combined.
        /// </summary>
        /// <param name="query"></param>
        public void Add(AQueryBuilder query)
        {
            mQueries.Add(query);
            mQuery = null;
        }

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            StringBuilder builder = new StringBuilder();
            builder.Append(mSpecifics.PreBlock);
            foreach (AQueryBuilder query in mQueries)
            {
                query.PrepareQuery();
                builder.Append(query.Query);
                builder.Append(mSpecifics.StatementTerminator);
                builder.Append("\r\n");
            }
            builder.Append(mSpecifics.PostBlock);
            mQuery = builder.ToString();
        }
    }
}

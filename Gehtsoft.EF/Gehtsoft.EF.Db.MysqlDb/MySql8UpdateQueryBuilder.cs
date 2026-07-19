using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    /// <summary>
    /// The <c>UPDATE</c> builder for MySQL 8. MySQL 8 rejects a subquery that reads the table being updated
    /// (error 1093: "You can't specify target table ... for update in FROM clause"). When an embedded SET
    /// subquery scans the update target, its <c>FROM &lt;target&gt; AS &lt;alias&gt;</c> is wrapped in a derived
    /// table (<c>FROM (SELECT * FROM &lt;target&gt;) AS &lt;alias&gt;</c>): the materialized copy is no longer the
    /// target table, so the restriction does not apply, while a correlated reference to the outer
    /// <c>&lt;target&gt;.column</c> (which has no trailing <c>AS</c>) is left untouched.
    /// </summary>
    internal sealed class MySql8UpdateQueryBuilder : UpdateQueryBuilder
    {
        public MySql8UpdateQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor)
            : base(specifics, descriptor)
        {
        }

        protected override string TransformSubquery(string subquerySql)
        {
            string target = mDescriptor.Name;
            string from = $"FROM {target} AS ";
            if (subquerySql.IndexOf(from, System.StringComparison.Ordinal) < 0)
                return subquerySql;
            return subquerySql.Replace(from, $"FROM (SELECT * FROM {target}) AS ");
        }
    }
}

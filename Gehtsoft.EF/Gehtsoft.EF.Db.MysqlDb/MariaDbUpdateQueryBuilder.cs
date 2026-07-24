using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    /// <summary>
    /// The <c>UPDATE</c> builder for MariaDB. MariaDB permits a subquery that reads the table being updated,
    /// so it needs no special handling beyond the base builder.
    /// </summary>
    internal sealed class MariaDbUpdateQueryBuilder : UpdateQueryBuilder
    {
        public MariaDbUpdateQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor)
            : base(specifics, descriptor)
        {
        }
    }
}

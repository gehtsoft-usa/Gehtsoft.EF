using System.Text;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    /// <summary>
    /// The dialect for MariaDB. MariaDB has no <c>SRID</c> attribute on a geometry column (it carries the SRID
    /// on the value), supports <c>DROP INDEX IF EXISTS</c>, and permits a subquery that reads the table being
    /// updated - so its builders are the straightforward forms.
    /// </summary>
    public sealed class MariaDbLanguageSpecifics : MysqlDbLanguageSpecifics
    {
        /// <summary>MariaDB has no geometry column <c>SRID</c> attribute; nothing is appended.</summary>
        protected override void AppendColumnSrid(StringBuilder builder, GeometryColumnMetadata geo)
        {
        }

        internal override DropIndexBuilder CreateDropIndexBuilder(string table, string name)
            => new MariaDbDropIndexBuilder(this, table, name);

        internal override UpdateQueryBuilder CreateUpdateQueryBuilder(TableDescriptor descriptor)
            => new MariaDbUpdateQueryBuilder(this, descriptor);
    }
}

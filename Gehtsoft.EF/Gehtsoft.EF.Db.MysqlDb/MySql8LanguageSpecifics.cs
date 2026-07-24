using System.Text;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    /// <summary>
    /// The dialect for Oracle MySQL (8.0+). It adds the <c>SRID</c> attribute to geometry columns, and its
    /// builders account for MySQL 8 lacking <c>DROP INDEX IF EXISTS</c> and rejecting a subquery that reads
    /// the table being updated (error 1093).
    /// </summary>
    public sealed class MySql8LanguageSpecifics : MysqlDbLanguageSpecifics
    {
        /// <summary>MySQL 8 binds a geometry column to a spatial reference system via the <c>SRID</c> attribute.</summary>
        protected override void AppendColumnSrid(StringBuilder builder, GeometryColumnMetadata geo)
            => builder.Append(" SRID ").Append(geo.Srid);

        internal override DropIndexBuilder CreateDropIndexBuilder(string table, string name)
            => new MySql8DropIndexBuilder(this, table, name);

        internal override UpdateQueryBuilder CreateUpdateQueryBuilder(TableDescriptor descriptor)
            => new MySql8UpdateQueryBuilder(this, descriptor);
    }
}

using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb
{
    internal class SqliteInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        public SqliteInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, table, selectQuery, ignoreAutoIncrement)
        {
        }

        protected override string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.BuildQuery(leftSide, autoIncrement));
            if (autoIncrement != null)
                builder.Append("; select last_insert_rowid();");
            return builder.ToString();
        }
    }
}
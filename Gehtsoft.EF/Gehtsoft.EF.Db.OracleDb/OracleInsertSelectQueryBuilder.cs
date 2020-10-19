using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Text;

namespace Gehtsoft.EF.Db.OracleDb
{
    class OracleInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        public OracleInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, table, selectQuery, ignoreAutoIncrement)
        {
        }

        protected override string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            if (autoIncrement != null)
            {
                builder.Append("BEGIN \r\n");
            }
            builder.Append(base.BuildQuery(leftSide, autoIncrement));
            if (autoIncrement != null)
            {
                builder.Append(";\r\n");
                builder.Append($"SELECT {mTable.Name}_{autoIncrement.Name}.currval");
                builder.Append(" INTO :");
                builder.Append(autoIncrement.Name);
                builder.Append($" FROM dual");
                builder.Append(";\r\n");
                builder.Append("END; \r\n");
            }
            return builder.ToString();
        }

        protected override bool HasExpressionForAutoincrement => true;

        protected override string ExpressionForAutoincrement(TableDescriptor.ColumnInfo autoIncrement)
        {
            return $"{mTable.Name}_{autoIncrement.Name}.nextval";
        }
    }
}

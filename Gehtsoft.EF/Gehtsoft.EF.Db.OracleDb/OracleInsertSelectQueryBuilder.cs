using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Text;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        public OracleInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, table, selectQuery, ignoreAutoIncrement)
        {
        }

        protected override string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            if (autoIncrement != null && !mIgnoreAutoIncrement)
            {
                builder.Append("BEGIN \r\n");
                var expr = $"{mTable.Name}_{autoIncrement.Name}.nextval";
                if (mSelect.Resultset[^1].Expression != expr)
                    mSelect.AddExpressionToResultset(expr, System.Data.DbType.Int32, false);
            }
            builder.Append(base.BuildQuery(leftSide, autoIncrement));
            if (autoIncrement != null && !mIgnoreAutoIncrement)
            {
                builder
                    .Append(";\r\n")
                    .Append("SELECT ")
                    .Append(mTable.Name)
                    .Append('_')
                    .Append(autoIncrement.Name)
                    .Append(".currval")
                    .Append(" INTO :")
                    .Append(autoIncrement.Name)
                    .Append(" FROM dual")
                    .Append(";\r\n")
                    .Append("END; \r\n");
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

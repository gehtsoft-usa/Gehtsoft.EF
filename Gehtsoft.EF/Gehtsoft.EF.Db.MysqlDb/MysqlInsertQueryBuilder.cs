using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    internal class MysqlInsertQueryBuilder : InsertQueryBuilder
    {
        private readonly bool mHasAutoId = false;

        public MysqlInsertQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, bool ignoreAutoIncrement) : base(specifics, descriptor, ignoreAutoIncrement)
        {
            if (ignoreAutoIncrement)
            {
                bool hasAutoId = false;
                foreach (TableDescriptor.ColumnInfo column in descriptor)
                {
                    if (column.Autoincrement && column.PrimaryKey)
                    {
                        hasAutoId = true;
                        break;
                    }
                }

                mHasAutoId = hasAutoId;
            }
        }

        protected override string BuildQuery(StringBuilder leftSide, StringBuilder rightSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.BuildQuery(leftSide, rightSide, autoIncrement));
            if (autoIncrement != null)
                builder.Append("; SELECT LAST_INSERT_ID();");
            else if (mHasAutoId)
            {
                /*
                builder
                    .Append("; SET @max = (SELECT MAX(")
                    .Append(mTable.PrimaryKey.Name)
                    .Append(")+1 FROM ")
                    .Append(mTable.Name)
                    .Append(')');

                builder
                    .Append("; SET @query = CONCAT('ALTER TABLE ")
                    .Append(mTable.Name)
                    .Append(" AUTO_INCREMENT = ', @max)");

                builder
                    .Append("; PREPARE stmt FROM @query")
                    .Append("; EXECUTE stmt")
                    .Append("; DEALLOCATE PREPARE stmt;");
                */
            }
            return builder.ToString();
        }
    }
}

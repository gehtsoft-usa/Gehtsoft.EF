using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    internal class MysqlInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        private readonly bool mHasAutoId = false;

        public MysqlInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, descriptor, selectQuery, ignoreAutoIncrement)
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
        protected override string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.BuildQuery(leftSide, autoIncrement));

            if (autoIncrement != null)
            {
                builder
                    .Append("; SELECT MAX(")
                    .Append(mTable.PrimaryKey.Name)
                    .Append(") FROM ")
                    .Append(mTable.Name)
                    .Append(';');
            }
            else if (mHasAutoId)
            {
                builder.Append("; SET @max = (SELECT MAX(")
                    .Append(mTable.PrimaryKey.Name)
                    .Append(")+1 FROM ")
                    .Append(mTable.Name)
                    .Append(')')
                    .Append("; SET @query = CONCAT('ALTER TABLE ")
                    .Append(mTable.Name)
                    .Append(" AUTO_INCREMENT = ', @max)")
                    .Append("; PREPARE stmt FROM @query")
                    .Append("; EXECUTE stmt")
                    .Append("; DEALLOCATE PREPARE stmt;");
            }
            return builder.ToString();
        }
    }
}

using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    class MysqlInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        private bool mHasAutoId = false;

        public MysqlInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, descriptor, selectQuery, ignoreAutoIncrement)
        {
            if (ignoreAutoIncrement)
            {
                bool hasAutoId = false;
                foreach (TableDescriptor.ColumnInfo column in descriptor)
                {
                    if (column.Autoincrement == true && column.PrimaryKey == true)
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
                builder.Append($"; SELECT MAX({mTable.PrimaryKey.Name}) FROM {mTable.Name};");
            else if (mHasAutoId)
            {
                builder.Append($"; SET @max = (SELECT MAX({mTable.PrimaryKey.Name})+1 FROM {mTable.Name})");
                builder.Append($"; SET @query = CONCAT('ALTER TABLE {mTable.Name} AUTO_INCREMENT = ', @max)");
                builder.Append($"; PREPARE stmt FROM @query");
                builder.Append($"; EXECUTE stmt");
                builder.Append($"; DEALLOCATE PREPARE stmt;");
            }
            return builder.ToString();
        }
    }
}

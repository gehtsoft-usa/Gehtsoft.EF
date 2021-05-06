using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    internal class MssqlInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        private readonly bool mHasAutoId = false;

        public MssqlInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, descriptor, selectQuery, ignoreAutoIncrement)
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
            if (mHasAutoId && mIgnoreAutoIncrement)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("SET IDENTITY_INSERT ").Append(mTable.Name).AppendLine(" ON;")
                    .Append(base.BuildQuery(leftSide, autoIncrement)).AppendLine(";")
                    .Append("SET IDENTITY_INSERT ").Append(mTable.Name).AppendLine(" OFF;");
                return builder.ToString();
            }
            else
            {
                if (autoIncrement == null)
                    return base.BuildQuery(leftSide, autoIncrement);
                else
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append("INSERT INTO ")
                        .Append(mTable.Name)
                        .Append(" ( ")
                        .Append(leftSide)
                        .Append(") ")
                        .Append(" OUTPUT INSERTED.")
                        .Append(autoIncrement.Name)
                        .Append(" ");
                    if (string.IsNullOrEmpty(mSelect.Query))
                        mSelect.PrepareQuery();
                    builder.Append(mSelect.Query);
                    return builder.ToString();
                }
            }
        }
    }
}

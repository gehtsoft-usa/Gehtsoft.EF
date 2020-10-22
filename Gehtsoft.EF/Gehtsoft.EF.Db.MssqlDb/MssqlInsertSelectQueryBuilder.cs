using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    class MssqlInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        private bool mHasAutoId = false;

        public MssqlInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, descriptor, selectQuery, ignoreAutoIncrement)
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
            if (mHasAutoId && mIgnoreAutoIncrement)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"SET IDENTITY_INSERT {mTable.Name} ON;");
                builder.AppendLine($"{base.BuildQuery(leftSide, autoIncrement)};");
                builder.AppendLine($"SET IDENTITY_INSERT {mTable.Name} OFF;");
                return builder.ToString();
            }
            else
            {
                if (autoIncrement == null)
                    return base.BuildQuery(leftSide, autoIncrement);
                else
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append("INSERT INTO ");
                    builder.Append(mTable.Name);
                    builder.Append(" ( ");
                    builder.Append(leftSide);
                    builder.Append(") ");
                    builder.Append(" OUTPUT INSERTED.");
                    builder.Append(autoIncrement.Name);
                    builder.Append(" ");
                    if (string.IsNullOrEmpty(mSelect.Query))
                        mSelect.PrepareQuery();
                    builder.Append(mSelect.Query);
                    return builder.ToString();
                }
            }
        }
    }
}

using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleDropTableBuilder : DropTableBuilder
    {
        public OracleDropTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        public override void PrepareQuery()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(mSpecifics.PreBlock);

            TableDescriptor.ColumnInfo autoIncrementColumn = null;
            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
                if (column.Autoincrement)
                {
                    autoIncrementColumn = column;
                    break;
                }

            if (autoIncrementColumn != null)
            {
                builder.Append(mSpecifics.PreBlock);
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("DROP SEQUENCE ")
                    .Append(mDescriptor.Name)
                    .Append('_')
                    .Append(autoIncrementColumn.Name);
                builder.Append(mSpecifics.PostQueryInBlock);
                builder.Append("EXCEPTION\r\n");
                builder.Append("  WHEN OTHERS THEN NULL;\r\n");
                builder.Append(mSpecifics.PostBlock);
            }

            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);
            builder.Append("DROP TABLE ").Append(mDescriptor.Name);
            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append("EXCEPTION\r\n");
            builder.Append("  WHEN OTHERS THEN NULL;\r\n");
            builder.Append(mSpecifics.PostBlock);

            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }
    }
}

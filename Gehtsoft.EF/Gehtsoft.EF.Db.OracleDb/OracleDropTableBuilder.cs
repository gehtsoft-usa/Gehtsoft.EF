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

        protected override void AppendDropTable(StringBuilder builder, TableDescriptor descriptor)
        {
            TableDescriptor.ColumnInfo autoIncrementColumn = null;
            foreach (TableDescriptor.ColumnInfo column in descriptor)
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
                    .Append(descriptor.Name)
                    .Append('_')
                    .Append(autoIncrementColumn.Name);
                builder.Append(mSpecifics.PostQueryInBlock);
                builder.Append("EXCEPTION\r\n");
                builder.Append("  WHEN OTHERS THEN NULL;\r\n");
                builder.Append(mSpecifics.PostBlock);
            }

            // Oracle does not remove USER_SDO_GEOM_METADATA rows when a table is dropped, so a geometry
            // table could not be recreated (ORA-13223 duplicate entry). Clear the metadata for every
            // geometry column first. Wrapped in EXECUTE IMMEDIATE, so the quotes are doubled.
            foreach (TableDescriptor.ColumnInfo column in descriptor)
            {
                if (column.Geometry == null)
                    continue;
                builder.Append(mSpecifics.PreBlock);
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("DELETE FROM USER_SDO_GEOM_METADATA WHERE TABLE_NAME = ''")
                    .Append(descriptor.Name.ToUpperInvariant())
                    .Append("'' AND COLUMN_NAME = ''")
                    .Append(column.Name.ToUpperInvariant())
                    .Append("''");
                builder.Append(mSpecifics.PostQueryInBlock);
                builder.Append("EXCEPTION\r\n");
                builder.Append("  WHEN OTHERS THEN NULL;\r\n");
                builder.Append(mSpecifics.PostBlock);
            }

            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);
            builder.Append("DROP TABLE ").Append(descriptor.Name);
            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append("EXCEPTION\r\n");
            builder.Append("  WHEN OTHERS THEN NULL;\r\n");
            builder.Append(mSpecifics.PostBlock);
        }
    }
}

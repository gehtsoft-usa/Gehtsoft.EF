using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlTableDdlBuilder : TableDdlBuilder
    {
        public MssqlTableDdlBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
        }

        public override void HandleAutoincrement(StringBuilder builder, TableDescriptor.ColumnInfo ci)
        {
            //do nothing for autoincrement
        }

        public override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
            {
                if (!alterTable)
                    builder.Append(", ");
                builder
                    .Append("CONSTRAINT ")
                    .Append(mDescriptor.Name)
                    .Append('_')
                    .Append(column.Name)
                    .Append("_fk FOREIGN KEY (")
                    .Append(column.Name)
                    .Append(") REFERENCES ")
                    .Append(column.ForeignTable.Name)
                    .Append('(')
                    .Append(column.ForeignTable.PrimaryKey.Name)
                    .Append(')');
            }
        }
    }
}

using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqliteDb
{
    internal class SqliteTableDdlBuilder : TableDdlBuilder
    {
        public SqliteTableDdlBuilder(SqlDb.SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
        }

        public override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            base.HandleColumnDDL(builder, column, alterTable);
            if (alterTable && column.ForeignKey && column.ForeignTable != column.Table)
            {
                builder
                    .Append(" REFERENCES ")
                    .Append(column.ForeignTable.Name)
                    .Append('(')
                    .Append(column.ForeignTable.PrimaryKey.Name)
                    .Append(')');
            }
        }

        public override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.ForeignKey && alterTable)
                return;
            base.HandlePostfixDDL(builder, column, alterTable);
        }

        public override bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return base.NeedIndex(column) || column.ForeignKey;
        }
    }
}

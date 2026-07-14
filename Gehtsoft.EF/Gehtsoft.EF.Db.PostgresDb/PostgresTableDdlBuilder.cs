using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Text;

namespace Gehtsoft.EF.Db.PostgresDb
{
    internal class PostgresTableDdlBuilder : TableDdlBuilder
    {
        public PostgresTableDdlBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }
        public override void HandleAutoincrement(StringBuilder builder, TableDescriptor.ColumnInfo ci)
        {
            //prevent for handling autoincremenet flag
        }

        public override void HandleGeometryAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            var indexes = column.Geometry.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE INDEX ")
                    .Append(mSpecifics.IndexName(column.Table.Name, indexes[i].Name))
                    .Append(" ON ")
                    .Append(column.Table.Name)
                    .Append(" USING GIST (")
                    .Append(column.Name)
                    .Append(')');
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }
    }
}


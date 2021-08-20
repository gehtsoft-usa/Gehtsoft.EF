using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public OracleAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override string AddColumnKeyword => "ADD";

        protected override TableDdlBuilder CreateDdlBuilder()
        {
            return new OracleTableDdlBuilder(mSpecifics, mDescriptor);
        }

        protected override void HandlePreDropQuery(TableDescriptor.ColumnInfo column)
        {
            base.HandlePreDropQuery(column);
            if (column.Autoincrement)
                mQueries.Add($"DROP SEQUENCE {mDescriptor.Name}_{column.Name}");
        }
    }
}

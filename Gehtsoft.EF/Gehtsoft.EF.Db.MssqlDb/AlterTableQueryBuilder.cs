using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    internal class MssqlAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public MssqlAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override TableDdlBuilder CreateDdlBuilder() => new MssqlTableDdlBuilder(mSpecifics, mDescriptor);

        protected override string AddColumnKeyword => "ADD";

        protected override void HandlePreDropQuery(TableDescriptor.ColumnInfo column)
        {
            base.HandlePreDropQuery(column);
            if (DdlBuilder.NeedIndex(column))
                mQueries.Add($"DROP INDEX {mDescriptor.Name}_{column.Name} ON {mDescriptor.Name}");

            if (column.ForeignKey && column.ForeignTable != column.Table)
                mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP CONSTRAINT {mDescriptor.Name}_{column.Name}_fk");
        }
    }
}

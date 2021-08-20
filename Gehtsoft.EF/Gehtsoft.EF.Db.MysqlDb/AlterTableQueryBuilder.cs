using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    internal class MysqlAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public MysqlAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override TableDdlBuilder CreateDdlBuilder()
        {
            return new MysqlTableDdlBuilder(mSpecifics, mDescriptor);
        }

        protected override void HandlePreDropQuery(TableDescriptor.ColumnInfo column)
        {
            if (column.ForeignKey)
                mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP FOREIGN KEY fk_{column.Table.Name}_{column.Name}");
        }
    }
}

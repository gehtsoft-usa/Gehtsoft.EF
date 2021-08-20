using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    internal class PostgresAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public PostgresAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override TableDdlBuilder CreateDdlBuilder()
        {
            return new PostgresTableDdlBuilder(mSpecifics, mDescriptor);
        }
    }
}

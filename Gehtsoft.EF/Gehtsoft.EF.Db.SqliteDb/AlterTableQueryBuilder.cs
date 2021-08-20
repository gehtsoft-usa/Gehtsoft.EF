using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.SqliteDb
{
    public class SqliteAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public SqliteAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override TableDdlBuilder CreateDdlBuilder()
        {
            return new SqliteTableDdlBuilder(mSpecifics, mDescriptor);
        }

        protected override void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
        }
    }
}

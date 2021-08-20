using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlCreateTableBuilder : CreateTableBuilder
    {
        public MssqlCreateTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
            DdlBuilder = new MssqlTableDdlBuilder(specifics, tableDescriptor);
        }
    }
}

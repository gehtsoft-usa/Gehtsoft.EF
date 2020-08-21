using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.SqliteDb
{
    class SqliteDbQuery : SqlDbQuery
    {
        public SqliteDbQuery(SqlDbConnection connection, DbCommand command, SqlDbLanguageSpecifics specifics) : base(connection, command, specifics)
        {

        }

        public override Stream GetStream(int column)
        {
            return new MemoryStream(GetValue<byte[]>(column));
        }
    }
}

using System;
using System.Data;
using System.Data.Common;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.OracleDb
{
    public class OracleDbQuery : SqlDbQuery
    {
        protected internal OracleDbQuery(OracleDbConnection connection, DbCommand command, SqlDbLanguageSpecifics specifics) : base(connection, command, specifics)
        {
        }

        protected override void HandleFieldName(ref string name)
        {
            name = name.ToUpper();
        }

        public override void BindNull(string name, DbType type)
        {
            if (type == DbType.Boolean)
                type = DbType.Int32;
            base.BindNull(name, type);
        }

        public override object GetValue(int column)
        {
            if (mReader.GetFieldType(column) == typeof(decimal))
            {
                if (mReader.IsDBNull(column))
                    return default(decimal);

                try
                {
                    return mReader.GetDecimal(column);
                }
                catch (InvalidCastException)
                {
                    return mReader.GetDouble(column);
                }
            }
            return base.GetValue(column);
        }
    }
}

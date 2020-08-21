using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        public override string TypeName(DbType dbtype, int size, int precision, bool autoincrement)
        {
            string type;
            switch (dbtype)
            {
                case DbType.String:
                    if (size == 0)
                        type = "text";
                    else
                        type = $"varchar({size})";
                    break;
                case DbType.Int16:
                    type = "smallint";
                    break;
                case DbType.Int32:
                    if (autoincrement)
                        type = "serial";
                    else
                        type = "integer";
                    break;
                case DbType.Int64:
                    if (autoincrement)
                        type = "bigserial";
                    else
                        type = "bigint";
                    break;
                case DbType.Date:
                    type = "date";
                    break;
                case DbType.DateTime:
                    type = "timestamp";
                    break;
                case DbType.Double:
                    if (size == 0 && precision == 0)
                        type = "double precision";
                    else if (size == 0 && precision != 0)
                        type = $"numeric(32, {precision})";
                    else
                        type = $"numeric({size}, {precision})";
                    break;
                case DbType.Binary:
                    type = "bytea";
                    break;
                case DbType.Boolean:
                    type = "boolean";
                    break;
                case DbType.Guid:
                    type = "uuid";
                    break;
                case DbType.Decimal:
                    if (size == 0 && precision == 0)
                        type = "double precision";
                    else if (size == 0 && precision != 0)
                        type = $"numeric(32, {precision})";
                    else
                        type = $"numeric({size}, {precision})";
                    break;
                default:
                    throw new InvalidOperationException("The type is not supported");
            }
            return type;
        }

        public override TransactionSupport SupportsTransactions => TransactionSupport.Nested;

        public override bool AllNonAggregatesInGroupBy => true;

        public override string GetSqlFunction(SqlFunctionId function, string[] args)
        {
            switch (function)
            {
                case SqlFunctionId.ToString:
                    return $"CAST({args[0]} AS VARCHAR)";
                case SqlFunctionId.ToInteger:
                    return $"CAST({args[0]} AS INT)";
                case SqlFunctionId.ToDouble:
                    return $"CAST({args[0]} AS NUMERIC)";
                case SqlFunctionId.ToDate:
                    return $"CAST({args[0]} AS DATE)";
                case SqlFunctionId.ToTimestamp:
                    return $"CAST({args[0]} AS TIMESTAMP)";
                default:
                    return base.GetSqlFunction(function, args);
            }
        }

        public override string FormatValue(object value)
        {
            if (value is bool)
                return (bool) value ? "TRUE" : "FALSE";
            if (value is DateTime)
            {
                DateTime dt = (DateTime) value;
                return $"CAST('{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}' AS DATE) ";
            }

            return base.FormatValue(value);
        }

        public override DateTime? MinDate => new DateTime(-4713, 1, 1);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31);
        public override DateTime? MinTimestamp => new DateTime(-4713, 1, 1);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31);


    }
}

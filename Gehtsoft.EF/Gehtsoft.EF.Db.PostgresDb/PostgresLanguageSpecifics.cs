using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        public override string TypeName(DbType type, int size, int precision, bool autoincrement)
        {
            string typeName;
            switch (type)
            {
                case DbType.String:
                    if (size == 0)
                        typeName = "text";
                    else
                        typeName = $"varchar({size})";
                    break;
                case DbType.Int16:
                    typeName = "smallint";
                    break;
                case DbType.Int32:
                    if (autoincrement)
                        typeName = "serial";
                    else
                        typeName = "integer";
                    break;
                case DbType.Int64:
                    if (autoincrement)
                        typeName = "bigserial";
                    else
                        typeName = "bigint";
                    break;
                case DbType.Date:
                    typeName = "date";
                    break;
                case DbType.DateTime:
                    typeName = "timestamp";
                    break;
                case DbType.Double:
                    if (size == 0 && precision == 0)
                        typeName = "double precision";
                    else if (size == 0 && precision != 0)
                        typeName = $"numeric(32, {precision})";
                    else
                        typeName = $"numeric({size}, {precision})";
                    break;
                case DbType.Binary:
                    typeName = "bytea";
                    break;
                case DbType.Boolean:
                    typeName = "boolean";
                    break;
                case DbType.Guid:
                    typeName = "uuid";
                    break;
                case DbType.Decimal:
                    if (size == 0 && precision == 0)
                        typeName = "double precision";
                    else if (size == 0 && precision != 0)
                        typeName = $"numeric(32, {precision})";
                    else
                        typeName = $"numeric({size}, {precision})";
                    break;
                default:
                    throw new InvalidOperationException("The type is not supported");
            }
            return typeName;
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
            if (value is bool b)
                return b ? "TRUE" : "FALSE";
            if (value is DateTime dt)
                return $"CAST('{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}' AS DATE) ";
            return base.FormatValue(value);
        }

        public override DateTime? MinDate => new DateTime(-4713, 1, 1);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31);
        public override DateTime? MinTimestamp => new DateTime(-4713, 1, 1);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31);
    }
}

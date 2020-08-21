using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Gehtsoft.EF.Db.OracleDb
{
    public class OracleDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        public override string TypeName(DbType dbtype, int size, int precision, bool autoincrement)
        {
            string type;
            switch (dbtype)
            {
                case DbType.String:
                    if (size == 0)
                        type = "clob";
                    else
                        type = $"nvarchar2({size})";
                    break;
                case DbType.Int16:
                    type = "number(8)";
                    break;
                case DbType.Int32:
                    type = "number(11)";
                    break;
                case DbType.Int64:
                    type = "number(38)";
                    break;
                case DbType.Date:
                    type = "date";
                    break;
                case DbType.DateTime:
                    type = "timestamp(3)";
                    break;
                case DbType.Double:
                    if (size == 0 && precision == 0)
                        type = "number(38, 8)";
                    else if (size == 0 && precision != 0)
                        type = $"number(38, {precision})";
                    else
                        type = $"number({size}, {precision})";
                    break;
                case DbType.Binary:
                    type = "blob";
                    break;
                case DbType.Boolean:
                    type = "number(1)";
                    break;
                case DbType.Guid:
                    type = "nvarchar2(40)";
                    break;
                case DbType.Decimal:
                    if (size == 0 && precision == 0)
                        type = "number(38, 8)";
                    else if (size == 0 && precision != 0)
                        type = $"number(38, {precision})";
                    else
                        type = $"number({size}, {precision})";
                    break;
                default:
                    throw new InvalidOperationException("The type is not supported");
            }

            return type;
        }

        public override void ToDbValue(ref object value, Type type, out DbType dbtype)
        {
            if (type == typeof(bool))
            {
                dbtype = DbType.Int32;
                value = (int)((bool)value ? 1 : 0);
            }
            else if (type == typeof(bool?))
            {
                dbtype = DbType.Int32;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = (int)((bool)value ? 1 : 0);
            }
            else if (type == typeof(int?))
            {
                dbtype = DbType.Int32;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = (int)(value);
            }
            else if (type == typeof(Guid))
            {
                dbtype = DbType.String;
                value = ((Guid)value).ToString("D");
            }
            else if (type == typeof(Guid?))
            {
                dbtype = DbType.String;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = ((Guid)(Guid?)value).ToString("D");
            }
            else
                base.ToDbValue(ref value, type, out dbtype);
        }

        public override object TranslateValue(object value, Type type)
        {
            if (value is OracleDecimal)
            {
                OracleDecimal odecimal = (OracleDecimal) value;
                if (type == typeof(int))
                    value = (int)odecimal.Value;
                else 
                    value = odecimal.Value;
            }

            if (type == typeof(bool))
            {
                if (value == null)
                    return default(bool);
                int t = (int) TranslateValue(value, typeof(int));
                return t != 0;
            }
            else if (type == typeof(bool?))
            {
                if (value == null)
                    return (bool?) null;
                int t = (int) TranslateValue(value, typeof(int));
                return (bool?) (t != 0);
            }
            else if (type == typeof(Guid))
            {
                string s = (string)TranslateValue(value, typeof(string));
                if (s == null)
                    return Guid.Empty;
                Guid guid;
                if (!Guid.TryParse(s, out guid))
                    return Guid.Empty;
                else
                    return guid;
            }
            else if (type == typeof(Guid?))
            {
                string s = (string)TranslateValue(value, typeof(string));
                if (s == null)
                    return (Guid?)null;
                Guid guid;
                if (!Guid.TryParse(s, out guid))
                    return (Guid?)Guid.Empty;
                else
                    return (Guid?)guid;
            }
            else
                return base.TranslateValue(value, type);
        }

        public override TransactionSupport SupportsTransactions => TransactionSupport.Plain;

        public override bool TerminateWithSemicolon => false;

        public override string PreBlock => "BEGIN \r\n";
        public override string PostBlock => "END; \r\n";
        public override string PreQueryInBlock => "EXECUTE IMMEDIATE '";
        public override string PostQueryInBlock => "';\r\n";
        public override string ParameterInQueryPrefix => ":";
        public override string ParameterPrefix => "";
        public override string TableAliasInSelect => "";

        public override AutoincrementReturnStyle AutoincrementReturnedAs => AutoincrementReturnStyle.Parameter;

        public override bool AllNonAggregatesInGroupBy => true;

        public override string GetSqlFunction(SqlFunctionId function, string[] args)
        {
            switch (function)
            {
                case SqlFunctionId.ToString:
                    return $"CAST({args[0]} AS VARCHAR2(1024))";
                case SqlFunctionId.ToInteger:
                    return $"CAST({args[0]} AS NUMBER)";
                case SqlFunctionId.ToDouble:
                    return $"CAST({args[0]} AS BINARY_DOUBLE)";
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
                return FormatValue((bool) value ? (int)1 : (int)0);
            if (value is string)
            {
                string s = (string) value;
                if (s.Contains("\r") || s.Contains("\n") || s.Contains("'"))
                    throw new ArgumentException("Illegal string content", nameof(value));
                return $"''{s}''";
            }
                
            if (value is DateTime)
            {
                DateTime dt = (DateTime) value;
                return $"DATE '{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}'";
            }

            return base.FormatValue(value);
        }

        public override DateTime? MinDate => new DateTime(-4712, 1, 1);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31);
        public override DateTime? MinTimestamp => new DateTime(-4712, 1, 1);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31);
        public override PagingSupport SupportsPaging => PagingSupport.Emulated;
    }
}

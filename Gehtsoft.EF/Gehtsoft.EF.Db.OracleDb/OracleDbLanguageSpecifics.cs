using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Gehtsoft.EF.Db.OracleDb
{
    public class OracleDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        public override string TypeName(DbType type, int size, int precision, bool autoincrement)
        {
            string typeName;
            switch (type)
            {
                case DbType.String:
                    if (size == 0)
                        typeName = "clob";
                    else
                        typeName = $"nvarchar2({size})";
                    break;
                case DbType.Int16:
                    typeName = "number(8)";
                    break;
                case DbType.Int32:
                    typeName = "number(11)";
                    break;
                case DbType.Int64:
                    typeName = "number(38)";
                    break;
                case DbType.Date:
                    typeName = "date";
                    break;
                case DbType.DateTime:
                    typeName = "timestamp(3)";
                    break;
                case DbType.Double:
                    if (size == 0 && precision == 0)
                        typeName = "number(38, 8)";
                    else if (size == 0 && precision != 0)
                        typeName = $"number(38, {precision})";
                    else
                        typeName = $"number({size}, {precision})";
                    break;
                case DbType.Binary:
                    typeName = "blob";
                    break;
                case DbType.Boolean:
                    typeName = "number(1)";
                    break;
                case DbType.Guid:
                    typeName = "nvarchar2(40)";
                    break;
                case DbType.Decimal:
                    if (size == 0 && precision == 0)
                        typeName = "number(38, 8)";
                    else if (size == 0 && precision != 0)
                        typeName = $"number(38, {precision})";
                    else
                        typeName = $"number({size}, {precision})";
                    break;
                default:
                    throw new InvalidOperationException("The type is not supported");
            }

            return typeName;
        }

        public override bool TypeToDb(Type type, out DbType dbtype)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool))
            {
                dbtype = DbType.Int32;
                return true;
            }
            if (type == typeof(Guid))
            {
                dbtype = DbType.String;
                return true;
            }

            return base.TypeToDb(type, out dbtype);
        }

        public override void ToDbValue(ref object value, Type type, out DbType dbtype)
        {
            if (type == typeof(bool))
            {
                dbtype = DbType.Int32;
                value = (bool)value ? 1 : 0;
            }
            else if (type == typeof(bool?))
            {
                dbtype = DbType.Int32;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = (bool)value ? 1 : 0;
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
            if (value is OracleDecimal odecimal)
            {
                if (type == typeof(int))
                    value = (int)odecimal.Value;
                else
                    value = odecimal.Value;
            }

            if (type == typeof(bool))
            {
                if (value == null)
                    return default(bool);
                int t = (int)TranslateValue(value, typeof(int));
                return t != 0;
            }
            else if (type == typeof(bool?))
            {
                if (value == null)
                    return (bool?)null;
                int t = (int)TranslateValue(value, typeof(int));
                return (bool?)(t != 0);
            }
            else if (type == typeof(Guid))
            {
                string s = (string)TranslateValue(value, typeof(string));
                if (s == null)
                    return Guid.Empty;
                if (!Guid.TryParse(s, out Guid guid))
                    return Guid.Empty;
                else
                    return guid;
            }
            else if (type == typeof(Guid?))
            {
                string s = (string)TranslateValue(value, typeof(string));
                if (s == null)
                    return (Guid?)null;
                if (!Guid.TryParse(s, out Guid guid))
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
            return function switch
            {
                SqlFunctionId.ToString => $"CAST({args[0]} AS VARCHAR2(1024))",
                SqlFunctionId.ToInteger => $"CAST({args[0]} AS NUMBER)",
                SqlFunctionId.ToDouble => $"CAST({args[0]} AS BINARY_DOUBLE)",
                SqlFunctionId.ToDate => $"CAST({args[0]} AS DATE)",
                SqlFunctionId.ToTimestamp => $"CAST({args[0]} AS TIMESTAMP)",
                _ => base.GetSqlFunction(function, args),
            };
        }

        public override string FormatValue(object value)
        {
            if (value is bool b)
                return FormatValue(b ? 1 : 0);
            if (value is string s)
            {
                if (s.Contains("\r") || s.Contains("\n") || s.Contains("'"))
                    throw new ArgumentException("Illegal string content", nameof(value));
                return $"''{s}''";
            }
            if (value is DateTime dt)
                return $"DATE '{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}'";
            return base.FormatValue(value);
        }

        public override DateTime? MinDate => new DateTime(-4712, 1, 1);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31);
        public override DateTime? MinTimestamp => new DateTime(-4712, 1, 1);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31);
        public override PagingSupport SupportsPaging => PagingSupport.Emulated;

        public override bool SupportFunctionsInIndexes => true;
    }
}

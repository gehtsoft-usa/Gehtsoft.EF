using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Gehtsoft.EF.Db.OracleDb
{
    public class OracleDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        /// <summary>
        /// The driver identifier of this dialect.
        /// </summary>
        public override string DbName => UniversalSqlDbFactory.ORACLE;

        /// <summary>
        /// Oracle (12.2+) supports JSON columns.
        /// </summary>
        public override bool SupportsJson => true;

        /// <summary>Oracle (Locator) provides the built-in <c>SDO_GEOMETRY</c> type.</summary>
        public override bool SupportsGeometry => true;

        /// <summary>
        /// Renders an Oracle geometry column: <c>SDO_GEOMETRY</c> (SRID and dimensionality live in the
        /// value / <c>USER_SDO_GEOM_METADATA</c>, not the column type).
        /// </summary>
        public override string GeometryColumnDDL(TableDescriptor.ColumnInfo column)
            => column.Nullable ? "SDO_GEOMETRY" : "SDO_GEOMETRY NOT NULL";

        /// <summary>
        /// Renders an Oracle JSON extraction using `JSON_VALUE(col, '$.path' RETURNING &lt;type&gt;)`.
        /// When <paramref name="forDdl"/> is `true` the path's single quotes are doubled, because
        /// index DDL is wrapped in an `EXECUTE IMMEDIATE '...'` block.
        /// </summary>
        public override string JsonExtract(string column, string path, DbType type, bool forDdl)
        {
            string q = forDdl ? "''" : "'";
            return $"JSON_VALUE({column}, {q}{path}{q} RETURNING {OracleJsonReturning(type)})";
        }

        // Oracle JSON_VALUE returns a JSON boolean as the text 'true'/'false' (RETURNING VARCHAR2).
        public override object JsonEncodeValue(DbType type, object value)
        {
            if (value != null && type == DbType.Boolean && value is bool b)
                return b ? "true" : "false";
            return base.JsonEncodeValue(type, value);
        }

        public override object JsonDecodeValue(DbType type, object value)
        {
            if (value != null && !(value is DBNull) && type == DbType.Boolean)
                return string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            return base.JsonDecodeValue(type, value);
        }

        private static string OracleJsonReturning(DbType type)
        {
            switch (type)
            {
                case DbType.Int16:
                case DbType.Int32:
                case DbType.Int64:
                case DbType.Decimal:
                case DbType.Currency:
                case DbType.Double:
                case DbType.Single:
                    return "NUMBER";      // JSON_VALUE RETURNING does not accept BINARY_DOUBLE/FLOAT
                case DbType.Boolean:
                    return "VARCHAR2(5)";      // 'true' / 'false'
                case DbType.DateTime:
                case DbType.DateTime2:
                case DbType.Date:
                    return "VARCHAR2(64)";     // ISO-8601 text
                default:
                    return "VARCHAR2(4000)";   // string / other
            }
        }

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
                case DbType.Single:
                case DbType.Double:
                    if (size == 0 && precision == 0)
                        typeName = "number";
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
                SqlFunctionId.Year => $"EXTRACT(YEAR FROM {args[0]})",
                SqlFunctionId.Month => $"EXTRACT(MONTH FROM {args[0]})",
                SqlFunctionId.Day => $"EXTRACT(DAY FROM {args[0]})",
                SqlFunctionId.Hour => $"EXTRACT(HOUR FROM {args[0]})",
                SqlFunctionId.Minute => $"EXTRACT(MINUTE FROM {args[0]})",
                SqlFunctionId.Second => $"EXTRACT(SECOND FROM {args[0]})",
                SqlFunctionId.Left => $"SUBSTR({args[0]}, 1, {args[1]})",
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

        public override DateTime? MinDate => new DateTime(-4712, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
        public override DateTime? MinTimestamp => new DateTime(-4712, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
        public override double MaxNumericValue => 1e126;
        public override PagingSupport SupportsPaging => PagingSupport.Emulated;
        public override bool SupportFunctionsInIndexes => true;
        public override bool SelectRequiresLimitWhenOffsetIsSet => true;
    }
}

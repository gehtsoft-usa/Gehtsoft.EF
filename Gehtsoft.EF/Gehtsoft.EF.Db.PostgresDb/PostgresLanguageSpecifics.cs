using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        /// <summary>
        /// The driver identifier of this dialect.
        /// </summary>
        public override string DbName => UniversalSqlDbFactory.POSTGRES;

        /// <summary>
        /// PostgreSQL supports JSON columns (jsonb).
        /// </summary>
        public override bool SupportsJson => true;

        /// <summary>PostGIS provides geometry columns.</summary>
        public override bool SupportsGeometry => true;

        /// <summary>Renders a PostGIS geometry column: <c>geometry(&lt;subtype&gt;&lt;Z/M&gt;,&lt;srid&gt;)</c>.</summary>
        public override string GeometryColumnDDL(TableDescriptor.ColumnInfo column)
        {
            GeometryColumnMetadata geo = column.Geometry;
            string type = $"geometry({GeometryDdlHelper.SubtypeName(geo.Subtype)}{GeometryDdlHelper.DimensionSuffix(geo.HasZ, geo.HasM)},{geo.Srid})";
            return column.Nullable ? type : type + " NOT NULL";
        }

        /// <summary>
        /// Renders a PostgreSQL JSON extraction. The JSON is stored as `text`, so it is cast to
        /// `jsonb` inline, the value is pulled with the `#&gt;&gt;` (text) path operator and then cast
        /// to the target type. `CREATE INDEX` is not wrapped in a quoted block, so
        /// <paramref name="forDdl"/> does not affect the expression.
        /// </summary>
        public override string JsonExtract(string column, string path, System.Data.DbType type, bool forDdl)
        {
            string extract = $"(({column})::jsonb #>> '{PostgresJsonPath(path)}')";
            string cast = PostgresJsonCast(type);
            return cast == null ? extract : $"({extract}::{cast})";
        }

        // "$.address.zip" -> "{address,zip}"; "$.children[0]" -> "{children,0}"
        private static string PostgresJsonPath(string path)
        {
            string p = path;
            if (p.StartsWith("$", System.StringComparison.Ordinal))
                p = p.Substring(1);
            // an array element "[N]" becomes a path step ",N"
            p = p.Replace("]", "").Replace("[", ".");
            p = p.Replace(".", ",").Trim(',');
            return "{" + p + "}";
        }

        private static string PostgresJsonCast(System.Data.DbType type)
        {
            switch (type)
            {
                case System.Data.DbType.Int16:
                case System.Data.DbType.Int32:
                    return "integer";
                case System.Data.DbType.Int64:
                    return "bigint";
                case System.Data.DbType.Double:
                    return "double precision";
                case System.Data.DbType.Single:
                    return "real";
                case System.Data.DbType.Decimal:
                case System.Data.DbType.Currency:
                    return "numeric";
                case System.Data.DbType.Boolean:
                    return "boolean";
                case System.Data.DbType.DateTime:
                case System.Data.DbType.DateTime2:
                case System.Data.DbType.Date:
                    // text->timestamp is not IMMUTABLE (cannot be indexed); index the ISO-8601 text,
                    // which sorts chronologically. Same as SQLite/Oracle (string comparison).
                    return null;
                default:
                    return null; // string / other -> keep as text
            }
        }

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
                case DbType.Single:
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
            return function switch
            {
                SqlFunctionId.ToString => $"CAST({args[0]} AS VARCHAR)",
                SqlFunctionId.ToInteger => $"CAST({args[0]} AS INT)",
                SqlFunctionId.ToDouble => $"CAST({args[0]} AS NUMERIC)",
                SqlFunctionId.ToDate => $"CAST({args[0]} AS DATE)",
                SqlFunctionId.ToTimestamp => $"CAST({args[0]} AS TIMESTAMP)",
                SqlFunctionId.Year => $"EXTRACT(YEAR FROM {args[0]})",
                SqlFunctionId.Month => $"EXTRACT(MONTH FROM {args[0]})",
                SqlFunctionId.Day => $"EXTRACT(DAY FROM {args[0]})",
                SqlFunctionId.Hour => $"EXTRACT(HOUR FROM {args[0]})",
                SqlFunctionId.Minute => $"EXTRACT(MINUTE FROM {args[0]})",
                SqlFunctionId.Second => $"EXTRACT(SECOND FROM {args[0]})",
                _ => base.GetSqlFunction(function, args),
            };
        }

        public override string FormatValue(object value)
        {
            if (value is bool b)
                return b ? "TRUE" : "FALSE";
            if (value is DateTime dt)
                return $"CAST('{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}' AS DATE)";
            return base.FormatValue(value);
        }

        public override DateTime? MinDate => new DateTime(-4713, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
        public override DateTime? MinTimestamp => new DateTime(-4713, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);

        public override bool SupportFunctionsInIndexes => true;

        public override object TranslateValue(object value, Type type)
        {
            if (value is System.DateOnly dol)
                value = dol.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            return base.TranslateValue(value, type);
        }
    }
}

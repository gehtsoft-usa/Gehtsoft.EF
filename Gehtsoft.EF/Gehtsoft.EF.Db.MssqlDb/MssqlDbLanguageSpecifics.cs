using System;
using System.Data;
using System.Runtime.ExceptionServices;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlDbLanguageSpecifics : SqlDbLanguageSpecifics
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
                        typeName = $"nvarchar({size})";
                    break;
                case DbType.Int16:
                    typeName = "smallint";
                    break;
                case DbType.Int32:
                    typeName = "int";
                    break;
                case DbType.Int64:
                    typeName = "bigint";
                    break;
                case DbType.Date:
                    typeName = "date";
                    break;
                case DbType.DateTime:
                    typeName = "datetime";
                    break;
                case DbType.Double:
                    if (size == 0 && precision == 0)
                        typeName = "float(53)";
                    else if (size == 0)
                        typeName = $"numeric({38}, {precision})";
                    else
                        typeName = $"numeric({size}, {precision})";
                    break;
                case DbType.Binary:
                    typeName = "image";
                    break;
                case DbType.Boolean:
                    typeName = "int";
                    break;
                case DbType.Guid:
                    typeName = "uniqueidentifier";
                    break;
                case DbType.Decimal:
                    if (size == 0 && precision == 0)
                        typeName = "float(53)";
                    else if (size == 0)
                        typeName = $"numeric({38}, {precision})";
                    else
                        typeName = $"numeric({size}, {precision})";
                    break;
                default:
                    throw new InvalidOperationException("The type is not supported");
            }

            if (autoincrement)
                typeName += " identity(1, 1)";
            return typeName;
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
                    value = ((bool)(bool?)value) ? 1 : 0;
            }
            else
                base.ToDbValue(ref value, type, out dbtype);
        }

        public override object TranslateValue(object value, Type type)
        {
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
            else
                return base.TranslateValue(value, type);
        }

        public override string GetSqlFunction(SqlFunctionId function, string[] args)
        {
            switch (function)
            {
                case SqlFunctionId.ToString:
                    return $"CONVERT(varchar, {args[0]})";
                case SqlFunctionId.ToInteger:
                    return $"CONVERT(int, {args[0]})";
                case SqlFunctionId.ToDouble:
                    return $"CONVERT(float, {args[0]})";
                case SqlFunctionId.ToDate:
                    return $"CONVERT(date, {args[0]})";
                case SqlFunctionId.ToTimestamp:
                    return $"CONVERT(datetime, {args[0]})";
                case SqlFunctionId.Concat:
                    {
                        StringBuilder builder = new StringBuilder("CONCAT(");
                        bool first = true;
                        foreach (string arg in args)
                        {
                            if (!first)
                                builder.Append(", ");
                            else
                                first = false;
                            builder.Append(arg);
                        }
                        builder.Append(")");
                        return builder.ToString();
                    }
                default:
                    return base.GetSqlFunction(function, args);
            }
        }

        public override string FormatValue(object value)
        {
            if (value is bool b)
                return FormatValue(b ? 1 : 0);
            if (value is DateTime dt)
                return $"{{d '{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}'}}";

            return base.FormatValue(value);
        }

        public override bool AllNonAggregatesInGroupBy => true;

        public override DateTime? MinDate => new DateTime(1, 1, 1);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31);
        public override DateTime? MinTimestamp => new DateTime(1753, 1, 1);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31);
    }
}

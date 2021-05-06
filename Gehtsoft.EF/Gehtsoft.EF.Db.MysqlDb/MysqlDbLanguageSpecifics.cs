using System;
using System.Data;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.MysqlDb
{
    public class MysqlDbLanguageSpecifics : SqlDbLanguageSpecifics
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
                        typeName = "double";
                    else if (size == 0 && precision != 0)
                        typeName = $"numeric(32, {precision})";
                    else
                        typeName = $"numeric({size}, {precision})";
                    break;
                case DbType.Binary:
                    typeName = "blob";
                    break;
                case DbType.Boolean:
                    typeName = "smallint";
                    break;
                case DbType.Guid:
                    typeName = "varchar(40)";
                    break;
                case DbType.Decimal:
                    if (size == 0 && precision == 0)
                        typeName = "double";
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

        public override void ToDbValue(ref object value, Type type, out DbType dbtype)
        {
            if (type == typeof(bool))
            {
                dbtype = DbType.Int16;
                value = (bool)value ? 1 : 0;
            }
            else if (type == typeof(bool?))
            {
                dbtype = DbType.Int16;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = ((bool)(bool?)value) ? 1 : 0;
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
            if (type == typeof(bool))
            {
                if (value == null)
                    return default(bool);
                short t = (short)TranslateValue(value, typeof(short));
                return t != 0;
            }
            else if (type == typeof(bool?))
            {
                if (value == null)
                    return (bool?)null;
                short t = (short)TranslateValue(value, typeof(short));
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

        public override string GetSqlFunction(SqlFunctionId function, string[] args)
        {
            switch (function)
            {
                case SqlFunctionId.ToString:
                    return $"CAST({args[0]} AS CHAR)";
                case SqlFunctionId.ToInteger:
                    return $"CAST({args[0]} AS SIGNED)";
                case SqlFunctionId.ToDouble:
                    return $"CAST({args[0]} AS DOUBLE)";
                case SqlFunctionId.ToDate:
                    return $"CAST({args[0]} AS DATE)";
                case SqlFunctionId.ToTimestamp:
                    return $"CAST({args[0]} AS DATETIME)";
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
                return $"'{dt.Year:0000}-{dt.Month:00}-{dt.Day:00}'";

            return base.FormatValue(value);
        }

        public override TransactionSupport SupportsTransactions => TransactionSupport.Plain;

        public override DateTime? MinDate => new DateTime(1000, 1, 1);
        public override DateTime? MaxDate => new DateTime(9999, 12, 31);
        public override DateTime? MinTimestamp => new DateTime(1000, 1, 1);
        public override DateTime? MaxTimestamp => new DateTime(9999, 12, 31);
    }
}

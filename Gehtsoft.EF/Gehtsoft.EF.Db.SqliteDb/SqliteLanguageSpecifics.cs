using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.SqliteDb
{
    public class SqliteDbLanguageSpecifics : SqlDbLanguageSpecifics
    {
        public override string TypeName(DbType type, int size, int precision, bool autoincrement)
        {
            switch (type)
            {
                case DbType.String:
                    return "TEXT";
                case DbType.Int16:
                    return "INTEGER";
                case DbType.Int32:
                    return "INTEGER";
                case DbType.Int64:
                    return "INTEGER";
                case DbType.Date:
                    return "REAL";
                case DbType.DateTime:
                    return "REAL";
                case DbType.Double:
                    return "REAL";
                case DbType.Binary:
                    return "BLOB";
                case DbType.Boolean:
                    return "INTEGER";
                case DbType.Guid:
                    return "TEXT";
                case DbType.Decimal:
                    return "REAL";
                default:
                    throw new InvalidOperationException("The type is not supported");
            }
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
                    value = (bool)(bool?)value ? 1 : 0;
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
            else if (type == typeof(DateTime))
            {
                dbtype = DbType.Double;
                DateTime dt = (DateTime)value;
                if (dt.Ticks == 0)
                    value = DBNull.Value;
                else
                    value = DateTimeTool.ToOADate(dt);
            }
            else if (type == typeof(DateTime?))
            {
                dbtype = DbType.Double;
                if (value == null)
                    value = DBNull.Value;
                else
                {
                    DateTime dt = (DateTime)(DateTime?)value;
                    if (dt.Ticks == 0)
                        value = DBNull.Value;
                    else
                        value = DateTimeTool.ToOADate(dt);
                }
            }
            else if (type == typeof(decimal))
            {
                dbtype = DbType.Double;
                value = (double)(decimal)value;
            }
            else if (type == typeof(decimal?))
            {
                dbtype = DbType.Double;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = (double)(decimal)value;
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
            else if (type == typeof(DateTime))
            {
                if (value == null)
                    return new DateTime(0);

                return DateTimeTool.FromOADate((double)TranslateValue(value, typeof(double)));
            }
            else if (type == typeof(DateTime?))
            {
                if (value == null)
                    return (DateTime?)null;

                return TranslateValue(value, typeof(DateTime));
            }
            else
                return base.TranslateValue(value, type);
        }

        public override TransactionSupport SupportsTransactions => TransactionSupport.Nested;

        public override bool OuterJoinSupported => false;

        public override bool RightJoinSupported => false;

        public override bool DropColumnSupported => false;

        public override bool ModifyColumnSupported => false;

        public override string GetSqlFunction(SqlFunctionId function, string[] args)
        {
            switch (function)
            {
                case SqlFunctionId.ToDate:
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                case SqlFunctionId.ToTimestamp:
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                default:
                    return base.GetSqlFunction(function, args);
            }
        }

        public override string FormatValue(object value)
        {
            if (value is bool b)
                return FormatValue(b ? 1 : 0);
            if (value is DateTime dt)
                return FormatValue(DateTimeTool.ToOADate(dt));
            return base.FormatValue(value);
        }

        public override bool SupportFunctionsInIndexes => true;
    }
}

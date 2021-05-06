using System;
using System.Reflection;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb
{
    public class SqlDbLanguageSpecifics
    {
        public virtual bool TerminateWithSemicolon
        {
            get { return true; }
        }

        public enum TransactionSupport
        {
            None,
            Plain,
            Nested,
        }

        public virtual TransactionSupport SupportsTransactions => TransactionSupport.Nested;

        public enum PagingSupport
        {
            None,
            Native,
            Emulated,
        }

        public virtual PagingSupport SupportsPaging => PagingSupport.Native;

        public virtual string PreBlock => "";

        public virtual string PostBlock => "";

        public virtual string PreQueryInBlock => "";

        public virtual string PostQueryInBlock => "";

        public virtual string ParameterInQueryPrefix => "@";

        public virtual string ParameterPrefix => "@";

        public virtual string TableAliasInSelect => "AS";

        public virtual bool AllNonAggregatesInGroupBy => false;

        public virtual bool RightJoinSupported => true;

        public virtual bool OuterJoinSupported => true;

        public virtual bool DropColumnSupported => true;

        public virtual bool ModifyColumnSupported => true;

        public virtual string TypeName(DbType type, int size, int precision, bool autoincrement)
        {
            throw new NotImplementedException();
        }

        public enum AutoincrementReturnStyle
        {
            Parameter,
            FirstResultset,
            SecondResultset,
        }

        public virtual AutoincrementReturnStyle AutoincrementReturnedAs => AutoincrementReturnStyle.FirstResultset;

        public virtual string GetLogOp(LogOp op)
        {
            if (op == LogOp.And)
                return " AND ";
            if (op == LogOp.Or)
                return " OR ";
            if (op == LogOp.Not)
                return " NOT (";
            if (op == (LogOp.Not | LogOp.Or))
                return " OR NOT (";
            if (op == (LogOp.Not | LogOp.And))
                return " AND NOT (";
            throw new EfSqlException(EfExceptionCode.UnknownOperator);
        }

        public virtual string CloseLogOp(LogOp op)
        {
            if ((op & LogOp.Not) == LogOp.Not)
                return ")";
            return "";
        }

        public virtual string GetOp(CmpOp op, string leftSide, string rightSide)
        {
            switch (op)
            {
                case CmpOp.Eq:
                    return $"{leftSide} = {rightSide}";

                case CmpOp.Neq:
                    return $"{leftSide} <> {rightSide}";

                case CmpOp.Gt:
                    return $"{leftSide} > {rightSide}";

                case CmpOp.Ge:
                    return $"{leftSide} >= {rightSide}";

                case CmpOp.Le:
                    return $"{leftSide} <= {rightSide}";

                case CmpOp.Ls:
                    return $"{leftSide} < {rightSide}";

                case CmpOp.Exists:
                    if (rightSide.StartsWith("("))
                        return $"EXISTS {rightSide}";
                    else
                        return $"EXISTS ({rightSide})";

                case CmpOp.NotExists:
                    if (rightSide.StartsWith("("))
                        return $"NOT EXISTS {rightSide}";
                    else
                        return $"NOT EXISTS ({rightSide})";

                case CmpOp.Like:
                    return $"{leftSide} LIKE {rightSide}";

                case CmpOp.In:
                    if (rightSide.StartsWith("("))
                        return $"{leftSide} IN {rightSide}";
                    else
                        return $"{leftSide} IN ({rightSide})";

                case CmpOp.NotIn:
                    if (rightSide.StartsWith("("))
                        return $"{leftSide} NOT IN {rightSide}";
                    else
                        return $"{leftSide} NOT IN ({rightSide})";

                case CmpOp.IsNull:
                    return $"{leftSide} IS NULL";

                case CmpOp.NotNull:
                    return $"{leftSide} IS NOT NULL";

                default:
                    throw new EfSqlException(EfExceptionCode.UnknownOperator);
            }
        }

        public virtual string GetAggFn(AggFn aggregate, string argument)
        {
            switch (aggregate)
            {
                case AggFn.Avg:
                    return $"AVG({argument})";

                case AggFn.Sum:
                    return $"SUM({argument})";

                case AggFn.Max:
                    return $"MAX({argument})";

                case AggFn.Min:
                    return $"MIN({argument})";

                case AggFn.Count:
                    if (string.IsNullOrEmpty(argument))
                        return "COUNT(*)";
                    else
                        return $"COUNT(DISTINCT {argument})";

                case AggFn.None:
                    return argument;

                default:
                    throw new EfSqlException(EfExceptionCode.UnknownOperator);
            }
        }

        public virtual void ToDbValue(ref object value, Type type, out DbType dbtype)
        {
            Type type1 = Nullable.GetUnderlyingType(type) ?? type;

            if (value == null)
            {
                value = DBNull.Value;
                type = type1;
            }
            else if (type1 != type)
            {
                value = Convert.ChangeType(value, type1);
                type = type1;
            }

            bool isEnum = type.IsEnum;
            if (isEnum)
            {
                type = typeof(int);
                value = Convert.ChangeType(value, typeof(int));
            }

            if (type == typeof(bool))
            {
                dbtype = DbType.Boolean;
            }
            else if (type == typeof(byte))
            {
                dbtype = DbType.Byte;
            }
            else if (type == typeof(short))
            {
                dbtype = DbType.Int16;
            }
            else if (type == typeof(int))
            {
                dbtype = DbType.Int32;
            }
            else if (type == typeof(long))
            {
                dbtype = DbType.Int64;
            }
            else if (type == typeof(double))
            {
                dbtype = DbType.Double;
            }
            else if (type == typeof(decimal))
            {
                dbtype = DbType.Decimal;
            }
            else if (type == typeof(DateTime))
            {
                dbtype = DbType.DateTime;
            }
            else if (type == typeof(Guid))
            {
                dbtype = DbType.Guid;
            }
            else if (type == typeof(byte[]))
            {
                dbtype = DbType.Binary;
            }
            else if (type == typeof(string))
            {
                dbtype = DbType.String;
            }
            else
            {
                throw new EfSqlException(EfExceptionCode.TypeIsUnsupported, type.FullName);
            }
        }

        public virtual object TranslateValue(object value, Type type)
        {
            if (value == null)
            {
                bool isValueType = type.IsValueType;
                if (isValueType)
                    return Activator.CreateInstance(type);
                else
                    return null;
            }
            else
            {
                type = Nullable.GetUnderlyingType(type) ?? type;
                bool isEnum = type.IsEnum;
                if (isEnum)
                {
                    if (!(value is int))
                        value = Convert.ChangeType(value, typeof(int));
                    value = Enum.ToObject(type, value);
                }
                else
                {
                    if (value.GetType() != type)
                    {
                        value = Convert.ChangeType(value, type);
                    }
                }
                return value;
            }
        }

        public virtual string GetSqlFunction(SqlFunctionId function, string[] args)
        {
            switch (function)
            {
                case SqlFunctionId.Avg:
                    return $"AVG({args[0]})";

                case SqlFunctionId.Sum:
                    return $"SUM({args[0]})";

                case SqlFunctionId.Min:
                    return $"MIN({args[0]})";

                case SqlFunctionId.Max:
                    return $"MAX({args[0]})";

                case SqlFunctionId.Count:
                    return "COUNT(*)";

                case SqlFunctionId.Like:
                    return $"{args[0]} LIKE {args[1]}";

                case SqlFunctionId.ToString:
                    return args[0];

                case SqlFunctionId.ToInteger:
                    return args[0];

                case SqlFunctionId.ToDouble:
                    return args[0];

                case SqlFunctionId.ToDate:
                    return args[0];

                case SqlFunctionId.ToTimestamp:
                    return args[0];

                case SqlFunctionId.Abs:
                    return $"ABS({args[0]})";

                case SqlFunctionId.Trim:
                    return $"TRIM({args[0]})";

                case SqlFunctionId.TrimLeft:
                    return $"LTRIM({args[0]})";

                case SqlFunctionId.TrimRight:
                    return $"RTRIM({args[0]})";

                case SqlFunctionId.Upper:
                    return $"UPPER({args[0]})";

                case SqlFunctionId.Lower:
                    return $"LOWER({args[0]})";

                case SqlFunctionId.Concat:
                    {
                        StringBuilder builder = new StringBuilder();
                        foreach (string arg in args)
                        {
                            if (builder.Length > 0)
                                builder.Append(" || ");
                            builder.Append(arg);
                        }

                        return builder.ToString();
                    }
            }
            return null;
        }

        public virtual string FormatValue(object value)
        {
            if (value is null)
                return "NULL";
            if (value is short sw)
                return (sw).ToString();
            if (value is int iv)
                return (iv).ToString();
            if (value is long lv)
                return (lv).ToString();
            if (value is float flt)
                return (flt).ToString();
            if (value is double dblV)
                return (dblV).ToString();
            if (value is string s)
            {
                if (s.Contains("\r") || s.Contains("\n") || s.Contains("'"))
                    throw new ArgumentException("Illegal string content", nameof(value));
                return $"'{s}'";
            }
            throw new ArgumentException("Unsupported type", nameof(value));
        }

        public virtual DateTime? MinDate { get; } = null;
        public virtual DateTime? MaxDate { get; } = null;
        public virtual DateTime? MinTimestamp { get; } = null;
        public virtual DateTime? MaxTimestamp { get; } = null;
    }

    public enum SqlFunctionId
    {
        ToString,
        ToDate,
        ToTimestamp,
        ToInteger,
        ToDouble,
        Like,
        Sum,
        Avg,
        Min,
        Max,
        Count,
        Abs,
        Trim,
        TrimLeft,
        TrimRight,
        Upper,
        Lower,
        Concat,
    }
}
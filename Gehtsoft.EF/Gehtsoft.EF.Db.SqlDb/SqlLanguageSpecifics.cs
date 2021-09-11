using System;
using System.Reflection;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Utils;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The set of the rules specific for a particular database driver.
    /// </summary>
    public abstract class SqlDbLanguageSpecifics
    {
        /// <summary>
        /// Flag indicating whether the queries must be terminated with semicolon.
        /// </summary>
        public virtual bool TerminateWithSemicolon
        {
            get { return true; }
        }

        /// <summary>
        /// The transaction support modes.
        /// </summary>
        public enum TransactionSupport
        {
            /// <summary>
            /// Transactions aren't supported
            /// </summary>
            None,
            /// <summary>
            /// Only plain (one at a time) transactions are supported.
            /// </summary>
            Plain,
            /// <summary>
            /// Nested transactions are supported.
            /// </summary>
            Nested,
        }

        /// <summary>
        /// The flag indicating whether the transactions are supported.
        /// </summary>
        public virtual TransactionSupport SupportsTransactions => TransactionSupport.Nested;

        /// <summary>
        /// The paging support modes.
        /// </summary>
        public enum PagingSupport
        {
            /// <summary>
            /// Paging is not supported.
            ///
            /// Take and Skip parameters of select query will be ignored.
            /// </summary>
            None,
            /// <summary>
            /// DB has native support for paging.
            /// </summary>
            Native,
            /// <summary>
            /// DB has means to emulate paging.
            /// </summary>
            Emulated,
        }

        /// <summary>
        /// The flag indicating whether paging is supported.
        /// </summary>
        public virtual PagingSupport SupportsPaging => PagingSupport.Native;

        /// <summary>
        /// The block of the code to use before the block of the SQL statements.
        /// </summary>
        public virtual string PreBlock => "";

        /// <summary>
        /// The block of the code to use after the block of the SQL statements.
        /// </summary>
        public virtual string PostBlock => "";

        /// <summary>
        /// The block of code to use before each SQL statement.
        /// </summary>
        public virtual string PreQueryInBlock => "";

        /// <summary>
        /// The block of code to use after each SQL statement.
        /// </summary>
        public virtual string PostQueryInBlock => "";

        /// <summary>
        /// The prefix of the parameter name inside the query.
        /// </summary>
        public virtual string ParameterInQueryPrefix => "@";

        /// <summary>
        /// The prefix of the parameter when pass to ADO.NET query object.
        /// </summary>
        public virtual string ParameterPrefix => "@";

        /// <summary>
        /// The keyword used to set table alias in `SELECT` queries.
        /// </summary>
        public virtual string TableAliasInSelect => "AS";

        /// <summary>
        /// The flag indicating whether dialect requires to list all column that aren't aggregated in `GROUP BY` clause.
        /// </summary>
        public virtual bool AllNonAggregatesInGroupBy => false;

        /// <summary>
        /// The flag indicating that right outer join is supported.
        /// </summary>
        public virtual bool RightJoinSupported => true;

        /// <summary>
        /// The flag indicating that full outer join is supported.
        /// </summary>
        public virtual bool OuterJoinSupported => true;

        /// <summary>
        /// The flag indicating that the drop column is supported.
        /// </summary>
        public virtual bool DropColumnSupported => true;

        /// <summary>
        /// The flag indicating that modify column is supported.
        /// </summary>
        public virtual bool ModifyColumnSupported => true;

        /// <summary>
        /// The flag indicating that functions may be used in the indexes.
        /// </summary>
        public virtual bool SupportFunctionsInIndexes => false;

        /// <summary>
        /// The flag indicating that indexes are created for foreign key automatically.
        /// </summary>
        public virtual bool IndexForFKCreatedAutomatically => false;

        /// <summary>
        /// Returns the name of the type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <param name="precision"></param>
        /// <param name="autoincrement"></param>
        /// <returns></returns>
        public abstract string TypeName(DbType type, int size, int precision, bool autoincrement);

        /// <summary>
        /// The modes how dialect returns auto-assigned primary keys in the `INSERT` statements.
        /// </summary>
        public enum AutoincrementReturnStyle
        {
            /// <summary>
            /// As output parameter.
            /// </summary>
            Parameter,
            /// <summary>
            /// In the first resultset.
            /// </summary>
            FirstResultset,
            /// <summary>
            /// In the second resultset.
            /// </summary>
            SecondResultset,
        }

        /// <summary>
        /// The flag indicating how autoincrement is returned.
        /// </summary>
        public virtual AutoincrementReturnStyle AutoincrementReturnedAs => AutoincrementReturnStyle.FirstResultset;

        /// <summary>
        /// Converts the logical operation to SQL code.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Closes the logical opertion in SQL code.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public virtual string CloseLogOp(LogOp op)
        {
            if ((op & LogOp.Not) == LogOp.Not)
                return ")";
            return "";
        }

        /// <summary>
        /// Gets comparison operation in SQL code.
        /// </summary>
        /// <param name="op"></param>
        /// <param name="leftSide"></param>
        /// <param name="rightSide"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets aggregate function in SQL code.
        /// </summary>
        /// <param name="aggregate"></param>
        /// <param name="argument"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Converts run-time type to DB type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="dbtype"></param>
        /// <returns></returns>
        public virtual bool TypeToDb(Type type, out DbType dbtype)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(short))
            {
                dbtype = DbType.Int16;
                return true;
            }
            else if (type == typeof(int) || type.IsEnum)
            {
                dbtype = DbType.Int32;
                return true;
            }
            else if (type == typeof(long))
            {
                dbtype = DbType.Int64;
                return true;
            }
            else if (type == typeof(bool))
            {
                dbtype = DbType.Boolean;
                return true;
            }
            else if (type == typeof(string))
            {
                dbtype = DbType.String;
                return true;
            }
            else if (type == typeof(double))
            {
                dbtype = DbType.Double;
                return true;
            }
            else if (type == typeof(decimal))
            {
                dbtype = DbType.Decimal;
                return true;
            }
            else if (type == typeof(DateTime))
            {
                dbtype = DbType.DateTime;
                return true;
            }
            else if (type == typeof(TimeSpan))
            {
                dbtype = DbType.Time;
                return true;
            }
            else if (type == typeof(byte[]))
            {
                dbtype = DbType.Binary;
                return true;
            }
            else if (type == typeof(Guid))
            {
                dbtype = DbType.Guid;
                return true;
            }
            else if (type == typeof(object))
            {
                dbtype = DbType.Object;
                return true;
            }
            else
            {
                dbtype = DbType.Object;
                return false;
            }
        }

        /// <summary>
        /// Converts the value of the type specified to DB-supported value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <param name="dbtype"></param>
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
            else if (type == typeof(TimeSpan))
            {
                dbtype = DbType.Time;
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

        /// <summary>
        /// Translates DB value to the expected value of the specified run-time type.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="type"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets SQL function in SQL code.
        /// </summary>
        /// <param name="function"></param>
        /// <param name="args"></param>
        /// <returns></returns>
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
                    if (args == null || args.Length < 1 || args[0] == null)
                        return "COUNT(*)";
                    else
                        return $"COUNT({args[0]})";

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

                case SqlFunctionId.Round:
                    if (args.Length == 1)
                        return $"ROUND({args[0]}, 0)";
                    else
                        return $"ROUND({args[0]}, {args[1]})";

                case SqlFunctionId.Year:
                    return $"YEAR({args[0]})";

                case SqlFunctionId.Month:
                    return $"MONTH({args[0]})";

                case SqlFunctionId.Day:
                    return $"DAY({args[0]})";

                case SqlFunctionId.Hour:
                    return $"HOUR({args[0]})";

                case SqlFunctionId.Minute:
                    return $"MINUTE({args[0]})";

                case SqlFunctionId.Second:
                    return $"SECOND({args[0]})";

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

                case SqlFunctionId.Left:
                    return $"LEFT({args[0]}, {args[1]})";

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

        /// <summary>
        /// Formats constant in SQL
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual string FormatValue(object value)
        {
            if (value is null)
                return "NULL";
            if (value is short sw)
                return (sw).ToString(CultureInfo.InvariantCulture);
            if (value is int iv)
                return (iv).ToString(CultureInfo.InvariantCulture);
            if (value is long lv)
                return (lv).ToString(CultureInfo.InvariantCulture);
            if (value is float flt)
                return (flt).ToString(CultureInfo.InvariantCulture);
            if (value is double dblV)
                return (dblV).ToString(CultureInfo.InvariantCulture);
            if (value is decimal dcV)
                return (dcV).ToString(CultureInfo.InvariantCulture);
            if (value is string s)
            {
                if (s.Contains("\r") || s.Contains("\n") || s.Contains("'"))
                    throw new ArgumentException("Illegal string content", nameof(value));
                return $"'{s}'";
            }
            throw new ArgumentException("Unsupported type", nameof(value));
        }

        /// <summary>
        /// Minimum date supported in date datatype.
        /// </summary>
        public virtual DateTime? MinDate { get; } = null;
        /// <summary>
        /// Maximum date supported in date datatype.
        /// </summary>
        public virtual DateTime? MaxDate { get; } = null;
        /// <summary>
        /// Minimum date supported in timestamp datatype.
        /// </summary>
        public virtual DateTime? MinTimestamp { get; } = null;
        /// <summary>
        /// Maximum date supported in timestamp datatype.
        /// </summary>
        public virtual DateTime? MaxTimestamp { get; } = null;
        /// <summary>
        /// The flag indicating whether the database compares string case sensitive by default.
        /// </summary>
        public virtual bool CaseSensitiveStringComparison => true;

        public virtual double MaxNumericValue => Double.MaxValue;

        /// <summary>
        /// Returns builder for group of parameters.
        /// </summary>
        /// <returns></returns>
        public virtual ParameterGroupQueryBuilder GetParameterGroupBuilder()
        {
            return new ParameterGroupQueryBuilder(this);
        }

        [DocgenIgnore]
        public virtual bool SelectRequiresLimitWhenOffsetIsSet => false;

        /// <summary>
        /// The flag indicating whether hierarchal queries are supported.
        /// </summary>
        public virtual bool HierarchicalQuerySupported => true;
    }

    [ExcludeFromCodeCoverage]
    internal class Sql92LanguageSpecifics : SqlDbLanguageSpecifics
    {
        public override string TypeName(DbType type, int size, int precision, bool autoincrement)
        {
            switch (type)
            {
                case DbType.Int32:
                    return "INTEGER";
                case DbType.Int64:
                    return "NUMERIC(19, 0)";
                case DbType.Double:
                case DbType.Decimal:
                    return $"NUMERIC({size}, {precision})";
                case DbType.Boolean:
                    return "VARCHAR(1)";
                case DbType.String:
                    return $"VARCHAR({size})";
                case DbType.Binary:
                    if (size > 0)
                        return $"BLOB({size})";
                    else
                        return "BLOB";
                case DbType.Date:
                    return "DATE";
                case DbType.DateTime:
                    return "TIMESTAMP";
                case DbType.Guid:
                    return "VARCHAR(40)";
                default:
                    throw new ArgumentException($"Type {type} is not supported in SQL92", nameof(type));
            }
        }

        public override bool TypeToDb(Type type, out DbType dbtype)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(bool) || type == typeof(Guid))
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
                dbtype = DbType.String;
                value = (bool)value ? "1" : "0";
            }
            else if (type == typeof(bool?))
            {
                dbtype = DbType.String;
                if (value == null)
                    value = DBNull.Value;
                else
                    value = (bool)value ? "1" : "0";
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
                string t = (string)TranslateValue(value, typeof(string));
                return t != "0";
            }
            else if (type == typeof(bool?))
            {
                if (value == null)
                    return (bool?)null;
                string t = (string)TranslateValue(value, typeof(string));
                return (bool?)(t != "0");
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

        public override bool HierarchicalQuerySupported => false;
    }

    /// <summary>
    /// The SQL function identifier.
    /// </summary>
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
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Second,
        Round,
        Left,
    }
}
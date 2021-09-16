using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// The list of SQL functions to be used in the query-related expression.
    ///
    /// Using this function forces the compiler to execute the function on the server side. For example, is `string.Upper()` is used
    /// and the arguments can be calculated locally, it will be calculated locally. But if `SqlFunction.Upper()` is used
    /// the function will always be executed on the server side.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class SqlFunction
    {
        public static string ToString(object value) => value?.ToString() ?? "null";

        public static DateTime ToDate(object value) => (DateTime)Convert.ChangeType(value, typeof(DateTime));

        public static DateTime ToTimestamp(object value) => (DateTime)Convert.ChangeType(value, typeof(DateTime));

        public static int ToInteger(object value) => (int)Convert.ChangeType(value, typeof(int));

        public static double ToDouble(object value) => (double)Convert.ChangeType(value, typeof(double));

        public static bool Like(object value, string mask)
        {
            Regex re = new Regex(mask.Replace(".", "\\.").Replace('_', '.').Replace("%", ".*"));
            return re.IsMatch(value.ToString());
        }

        private static dynamic ThrowNotUseLocally()
        {
            throw new InvalidOperationException("Sql function shall not be used locally");
        }

        public static T Sum<T>(T value)
        {
            return ThrowNotUseLocally();
        }

        public static T Avg<T>(T value)
        {
            return ThrowNotUseLocally();
        }

        public static T Min<T>(T value)
        {
            return ThrowNotUseLocally();
        }

        public static T Max<T>(T value)
        {
            return ThrowNotUseLocally();
        }

        public static int Count()
        {
            return ThrowNotUseLocally();
        }

        public static int Abs(int value) => Math.Abs(value);

        public static double Abs(double value) => Math.Abs(value);
        public static double Round(double value, int count = 0) => Math.Round(value, count);

        public static string Trim(string value) => value.Trim();

        public static string Left(string value, int count) => value.Substring(0, count);

        public static int Length(string value) => value.Length;

        public static string TrimLeft(string value) => value.TrimStart();

        public static string TrimRight(string value) => value.TrimEnd();

        public static string Upper(string value) => value.ToUpper();

        public static string Lower(string value) => value.ToLower();

        public static int Year(DateTime dt) => dt.Year;
        public static int Month(DateTime dt) => dt.Month;
        public static int Day(DateTime dt) => dt.Day;
        public static int Hour(DateTime dt) => dt.Hour;
        public static int Minute(DateTime dt) => dt.Minute;
        public static int Second(DateTime dt) => dt.Second;

        public static bool In(object value, SelectEntitiesQueryBase query)
        {
            return ThrowNotUseLocally();
        }

        public static bool NotIn(object value, SelectEntitiesQueryBase query)
        {
            return ThrowNotUseLocally();
        }

        public static bool Exists(SelectEntitiesQueryBase query)
        {
            return ThrowNotUseLocally();
        }

        public static bool NotExists(SelectEntitiesQueryBase query)
        {
            return ThrowNotUseLocally();
        }

        public static bool In(object value, AQueryBuilder query)
        {
            return ThrowNotUseLocally();
        }

        public static bool NotIn(object value, AQueryBuilder query)
        {
            return ThrowNotUseLocally();
        }

        public static bool Exists(AQueryBuilder query)
        {
            return ThrowNotUseLocally();
        }

        public static bool NotExists(AQueryBuilder query)
        {
            return ThrowNotUseLocally();
        }

        public static T Value<T>(ConditionEntityQueryBase.InQueryName inQueryName)
        {
            return ThrowNotUseLocally();
        }

        public static T Value<T>(SelectEntitiesQueryBase subquery)
        {
            return ThrowNotUseLocally();
        }

        public static string Concat(string value1, string value2)
        {
            return value1 + value2;
        }

        public static string Concat(string value1, string value2, string value3)
        {
            return value1 + value2 + value3;
        }

        public static string Concat(string value1, string value2, string value3, string value4)
        {
            return value1 + value2 + value3 + value4;
        }
    }
}


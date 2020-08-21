using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    public static class SqlFunction
    {
        public static string ToString(object value) => value?.ToString() ?? "null";

        public static DateTime ToDate(object value) => (DateTime) Convert.ChangeType(value, typeof(DateTime));

        public static DateTime ToTimestamp(object value) => (DateTime) Convert.ChangeType(value, typeof(DateTime));

        public static int ToInteger(object value) => (int) Convert.ChangeType(value, typeof(int));

        public static double ToDouble(object value) => (double) Convert.ChangeType(value, typeof(double));

        public static bool Like(object value, string mask)
        {
            Regex re = new Regex(mask.Replace(".", "\\.").Replace('%', '.').Replace("*", ".*"));
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

        public static string Trim(string value) => value.Trim();

        public static string TrimLeft(string value) => value.TrimStart();

        public static string TrimRight(string value) => value.TrimEnd();

        public static string Upper(string value) => value.ToUpper();

        public static string Lower(string value) => value.ToLower();

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


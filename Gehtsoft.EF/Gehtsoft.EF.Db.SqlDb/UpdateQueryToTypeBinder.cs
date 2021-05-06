using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb
{
    public class UpdateQueryToTypeBinderRule
    {
        public string ParameterName { get; set; }
        public string PropertyName { get; set; }
        public IPropertyAccessor PropertyInfo { get; set; }
        public IPropertyAccessor PkPropertyInfo { get; set; }
        public DbType DbType { get; set; }
        public bool OutputOnInsert { get; set; }
        public int Size { get; set; }
    }

    public static class UpdateQueryTruncationRules
    {
        public static bool Enabled { get; set; } = false;
        public static bool TruncateStrings { get; set; } = true;
        public static bool TruncateNumbers { get; set; } = true;
        public static bool TruncateDates { get; set; } = true;
        public static DateTime MaximumDate { get; set; } = new DateTime(9999, 12, 31);
        public static DateTime MinimumDate { get; set; } = new DateTime(1, 1, 1);

        public static object Truncate(DbType type, int size, object value)
        {
            if (!Enabled || value == null)
                return value;

            if (value is string && size > 0 && TruncateStrings && type == DbType.String)
            {
                string s = value as string;
                if (s.Length > size)
                    s = s.Substring(0, size);
                return s;
            }

            if (type == DbType.Double && size > 0 && TruncateNumbers)
            {
                double d;
                if (value is double x)
                    d = x;
                else
                    d = Convert.ToDouble(value);
                double m = Math.Pow(10.0, size) - 1;
                if (d > m)
                    return m;
                else if (d < -m)
                    return -m;
                return d;
            }

            if (type == DbType.Decimal && size > 0 && TruncateNumbers)
            {
                decimal d;
                if (value is decimal x)
                    d = x;
                else
                    d = Convert.ToDecimal(value);
                decimal m = (decimal)Math.Pow(10.0, size) - 1;
                if (d > m)
                    return m;
                else if (d < -m)
                    return -m;
                return d;
            }

            if ((type == DbType.DateTime || type == DbType.Date || type == DbType.DateTime2) && (value is DateTime dt) && TruncateDates)
            {
                if (dt > MaximumDate)
                    return MaximumDate;
                else if (dt < MinimumDate)
                    return MinimumDate;
                return dt;
            }
            return value;
        }
    }

    public class UpdateQueryToTypeBinder
    {
        private readonly Type mType;
        private readonly List<UpdateQueryToTypeBinderRule> mRules = new List<UpdateQueryToTypeBinderRule>();
        private UpdateQueryToTypeBinderRule mAutoPkRule = null;

        public UpdateQueryToTypeBinder(Type type)
        {
            mType = type;
        }

        protected void AddBinding(string parameterName, string propertyName, IPropertyAccessor propertyInfo, IPropertyAccessor pkPropertyInfo, DbType dbType, int size, bool outputOnInsert)
        {
            UpdateQueryToTypeBinderRule rule = new UpdateQueryToTypeBinderRule()
            {
                ParameterName = parameterName,
                PropertyName = propertyName,
                PropertyInfo = propertyInfo,
                PkPropertyInfo = pkPropertyInfo,
                OutputOnInsert = outputOnInsert,
                Size = size,
                DbType = dbType,
            };

            mRules.Add(rule);

            if (rule.OutputOnInsert)
                mAutoPkRule = rule;
        }

        public void AddBinding(string parameterName, string propertyName, DbType dbType, int size, bool outputOnInsert = false)
        {
            PropertyAccessor propertyInfo = new PropertyAccessor(mType.GetProperty(propertyName));
            AddBinding(parameterName, propertyName, propertyInfo, null, dbType, size, outputOnInsert);
        }

        public void AddBinding(string parameterName, IPropertyAccessor propertyInfo, DbType dbType, int size, bool outputOnInsert = false)
        {
            AddBinding(parameterName, propertyInfo.Name, propertyInfo, null, dbType, size, outputOnInsert);
        }

        internal void AddBinding(string parameterName, IPropertyAccessor propertyInfo, IPropertyAccessor pkPropertyInfo, DbType dbType, int size, bool outputOnInsert = false)
        {
            AddBinding(parameterName, propertyInfo.Name, propertyInfo, pkPropertyInfo, dbType, size, outputOnInsert);
        }

        public void AutoBind(TableDescriptor tableDescriptor, string dbprefix = null, string typeprefix = null)
        {
            foreach (TableDescriptor.ColumnInfo column in tableDescriptor)
            {
                string name = column.Name;
                if (dbprefix != null && name.StartsWith(dbprefix))
                    name = name.Substring(dbprefix.Length);
                if (typeprefix != null)
                    name += typeprefix;
                PropertyInfo propertyInfo = mType.GetProperty(name);
                AddBinding(column.Name, name, new PropertyAccessor(propertyInfo), null, column.DbType, column.Size, column.Autoincrement && column.PrimaryKey);
            }
        }

        public virtual void BindAndExecute(SqlDbQuery query, object value, bool? insert = null) =>
            BindAndExecuteCore(false, query, value, insert, null)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

        public virtual Task BindAndExecuteAsync(SqlDbQuery query, object value, bool? insert = null, CancellationToken? token = null) => BindAndExecuteCore(false, query, value, insert, token);

        protected virtual async Task BindAndExecuteCore(bool sync, SqlDbQuery query, object value, bool? insert, CancellationToken? token)
        {
            bool isInsert;

            if (insert == null)
                isInsert = query.IsInsert;
            else
                isInsert = (bool)insert;

            foreach (UpdateQueryToTypeBinderRule rule in mRules)
            {
                if (rule.OutputOnInsert && isInsert)
                    continue;

                object v = rule.PropertyInfo.GetValue(value);
                if (v == null)
                    query.BindNull(rule.ParameterName, rule.DbType);
                else
                {
                    Type t;
                    if (rule.PkPropertyInfo != null)
                    {
                        v = rule.PkPropertyInfo.GetValue(v);
                        t = rule.PkPropertyInfo.PropertyType;
                    }
                    else if (UpdateQueryTruncationRules.Enabled)
                    {
                        v = UpdateQueryTruncationRules.Truncate(rule.DbType, rule.Size, value);
                        t = v.GetType();
                    }
                    else
                        t = rule.PropertyInfo.PropertyType;

                    if (t == typeof(object) && v != null)
                        t = v.GetType();

                    query.BindParam(rule.ParameterName, ParameterDirection.Input, v, t);
                }
            }

            bool executeNoData = true;

            if (isInsert && mAutoPkRule != null)
            {
                if (query.LanguageSpecifics.AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutputParam(mAutoPkRule.ParameterName, mAutoPkRule.DbType);
                else
                    executeNoData = false;
            }

            if (sync)
            {
                if (executeNoData)
                    query.ExecuteNoData();
                else
                    query.ExecuteReader();
            }
            else
            {
                if (token == null)
                {
                    if (executeNoData)
                        await query.ExecuteNoDataAsync();
                    else
                        await query.ExecuteReaderAsync();
                }
                else
                {
                    if (executeNoData)
                        await query.ExecuteNoDataAsync(token.Value);
                    else
                        await query.ExecuteReaderAsync(token.Value);
                }
            }

            if (isInsert && mAutoPkRule != null)
            {
                object v;
                if (query.LanguageSpecifics.AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                {
                    v = query.GetParamValue(mAutoPkRule.ParameterName, mAutoPkRule.PropertyInfo.PropertyType);
                }
                else
                {
                    if (sync)
                        query.ReadNext();
                    else
                    {
                        if (token == null)
                            await query.ReadNextAsync();
                        else
                            await query.ReadNextAsync(token.Value);
                    }
                    v = query.GetValue(0, mAutoPkRule.PropertyInfo.PropertyType);
                }
                mAutoPkRule.PropertyInfo.SetValue(value, v);
            }
        }
    }
}
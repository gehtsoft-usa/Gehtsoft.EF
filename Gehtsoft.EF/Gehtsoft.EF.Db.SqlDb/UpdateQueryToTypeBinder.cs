using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The rule for an update query binder.
    /// </summary>
    public class UpdateQueryToTypeBinderRule
    {
        /// <summary>
        /// The name of the parameter in the query
        /// </summary>
        public string ParameterName { get; set; }
        /// <summary>
        /// The name of the property
        /// </summary>
        public string PropertyName { get; set; }
        /// <summary>
        /// The property accessor
        /// </summary>
        public IPropertyAccessor PropertyInfo { get; set; }
        /// <summary>
        /// For foreign key field - the property accessor to the primary key of the associated entity
        /// </summary>
        public IPropertyAccessor PkPropertyInfo { get; set; }
        /// <summary>
        /// The type of the parameter.
        /// </summary>
        public DbType DbType { get; set; }
        /// <summary>
        /// The flag indicating that the parameter will be output for insert operation.
        /// </summary>
        public bool OutputOnInsert { get; set; }
        /// <summary>
        /// The size of the value.
        /// </summary>
        public int Size { get; set; }
    }

    /// <summary>
    /// The controller of data truncation.
    ///
    /// Use <see cref="DefaultUpdateQueryTruncationController"/> as a default implementation of the interface.
    ///
    /// Use <see cref="UpdateQueryTruncationRules"/> to set truncation rules.
    /// </summary>
    public interface IUpdateQueryTruncationController
    {
        /// <summary>
        /// Truncates the value to the parameters of the type.
        ///
        /// The controller truncates the value so it fits into
        /// the column definition and supported value range.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        object Truncate(DbType type, int size, object value);
    }

    /// <summary>
    /// The default implementation of the data truncation controller.
    /// </summary>
    public class DefaultUpdateQueryTruncationController : IUpdateQueryTruncationController
    {
        /// <summary>
        /// The flag indicating whether the controller should truncate strings.
        /// </summary>
        public bool TruncateStrings { get; set; } = true;
        /// <summary>
        /// The flag indicating whether the controller should truncate numbers.
        /// </summary>
        public bool TruncateNumbers { get; set; } = true;
        /// <summary>
        /// The flag indicating whether the controller should truncate dates.
        /// </summary>
        public bool TruncateDates { get; set; } = true;
        /// <summary>
        /// The maximum date value supported
        /// </summary>
        public DateTime MaximumDate { get; set; } = new DateTime(9999, 12, 31);
        /// <summary>
        /// The minimum date value supported.
        /// </summary>
        public DateTime MinimumDate { get; set; } = new DateTime(1, 1, 1);

        /// <summary>
        /// Truncates the value to the parameters of the type.
        ///
        /// The controller truncates the value so it fits into
        /// the column definition and supported value range.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="size"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual object Truncate(DbType type, int size, object value)
        {
            if (value == null)
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

            if (type == DbType.Int16 && size > 0 && TruncateNumbers)
            {
                double d;
                if (value is double x)
                    d = x;
                else
                    d = Convert.ToDouble(value);
                double m = Math.Pow(10.0, size) - 1;
                if (d > m)
                    return (short)m;
                else if (d < -m)
                    return (short)-m;
                return value;
            }

            if (type == DbType.Int32 && size > 0 && TruncateNumbers)
            {
                double d;
                if (value is double x)
                    d = x;
                else
                    d = Convert.ToDouble(value);
                double m = Math.Pow(10.0, size) - 1;
                if (d > m)
                    return (int)m;
                else if (d < -m)
                    return (int)-m;
                return value;
            }

            if (type == DbType.Int64 && size > 0 && TruncateNumbers)
            {
                double d;
                if (value is double x)
                    d = x;
                else
                    d = Convert.ToDouble(value);
                double m = Math.Pow(10.0, size) - 1;
                if (d > m)
                    return (long)m;
                else if (d < -m)
                    return (long)-m;
                return value;
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

    /// <summary>
    /// The truncation rules manager.
    /// </summary>
    public class UpdateQueryTruncationRules
    {
        /// <summary>
        /// Default truncation rules.
        /// </summary>
        public IUpdateQueryTruncationController DefaultRules { get; set; } = null;

        private readonly ConcurrentDictionary<string, IUpdateQueryTruncationController> mRules = new ConcurrentDictionary<string, IUpdateQueryTruncationController>();

        /// <summary>
        /// Gets the truncation rules for the specified connection.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public IUpdateQueryTruncationController this[string connectionString]
        {
            get
            {
                if (mRules.TryGetValue(connectionString, out var x))
                    return x;
                return DefaultRules;
            }
        }

        /// <summary>
        /// Enables truncation for the connections with the specified connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="truncationController"></param>
        public void EnableTruncation(string connectionString, IUpdateQueryTruncationController truncationController) => mRules.TryAdd(connectionString, truncationController);

        /// <summary>
        /// Disables truncation for the connection with the specified connection string.
        /// </summary>
        /// <param name="connectionString"></param>
        public void DisableTruncation(string connectionString) => mRules.TryRemove(connectionString, out _);

        /// <summary>
        /// Returns an instance of the singleton truncate rule manager.
        /// </summary>
        public static UpdateQueryTruncationRules Instance { get; } = new UpdateQueryTruncationRules();
    }

    /// <summary>
    /// The binder of the entity data into an insert or update query.
    /// </summary>
    public class UpdateQueryToTypeBinder
    {
        private readonly Type mType;
        private readonly List<UpdateQueryToTypeBinderRule> mRules = new List<UpdateQueryToTypeBinderRule>();
        private UpdateQueryToTypeBinderRule mAutoPkRule = null;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">The type that contains the data.</param>
        public UpdateQueryToTypeBinder(Type type)
        {
            mType = type;
        }

        /// <summary>
        /// Adds binding using a property accessor for foreign key property.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="pkPropertyInfo"></param>
        /// <param name="dbType"></param>
        /// <param name="size"></param>
        /// <param name="outputOnInsert"></param>
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

        /// <summary>
        /// Adds binding using property name.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="propertyName"></param>
        /// <param name="dbType"></param>
        /// <param name="size"></param>
        /// <param name="outputOnInsert"></param>
        public void AddBinding(string parameterName, string propertyName, DbType dbType, int size, bool outputOnInsert = false)
        {
            PropertyAccessor propertyInfo = new PropertyAccessor(mType.GetProperty(propertyName));
            AddBinding(parameterName, propertyName, propertyInfo, null, dbType, size, outputOnInsert);
        }

        /// <summary>
        /// Adds binding using property accessor.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="dbType"></param>
        /// <param name="size"></param>
        /// <param name="outputOnInsert"></param>
        public void AddBinding(string parameterName, IPropertyAccessor propertyInfo, DbType dbType, int size, bool outputOnInsert = false)
        {
            AddBinding(parameterName, propertyInfo.Name, propertyInfo, null, dbType, size, outputOnInsert);
        }

        /// <summary>
        /// Adds binding using a property accessor for foreign key property.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="propertyInfo"></param>
        /// <param name="pkPropertyInfo"></param>
        /// <param name="dbType"></param>
        /// <param name="size"></param>
        /// <param name="outputOnInsert"></param>
        internal void AddBinding(string parameterName, IPropertyAccessor propertyInfo, IPropertyAccessor pkPropertyInfo, DbType dbType, int size, bool outputOnInsert = false)
        {
            AddBinding(parameterName, propertyInfo.Name, propertyInfo, pkPropertyInfo, dbType, size, outputOnInsert);
        }

        /// <summary>
        /// Binds all columns in the table descriptor.
        ///
        /// The binding defines the parameter names the same as column names.
        /// </summary>
        /// <param name="tableDescriptor"></param>
        /// <param name="dbprefix"></param>
        /// <param name="typeprefix"></param>
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
                AddBinding(column.Name, name, column.PropertyAccessor ?? new PropertyAccessor(propertyInfo), null, column.DbType, column.Size, column.Autoincrement && column.PrimaryKey);
            }
        }

        /// <summary>
        /// Binds parameters and executes the query.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <param name="insert"></param>
        public virtual void BindAndExecute(SqlDbQuery query, object value, bool? insert = null) =>
            BindAndExecuteCore(false, query, value, insert, null)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

        /// <summary>
        /// Binds parameters and executes the query asynchronously.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="value"></param>
        /// <param name="insert"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task BindAndExecuteAsync(SqlDbQuery query, object value, bool? insert = null, CancellationToken? token = null) => BindAndExecuteCore(false, query, value, insert, token);

        protected virtual async Task BindAndExecuteCore(bool sync, SqlDbQuery query, object value, bool? insert, CancellationToken? token)
        {
            bool isInsert;
            var truncateController = UpdateQueryTruncationRules.Instance[query.Connection.Connection.ConnectionString];

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
                    else if (truncateController != null)
                    {
                        v = truncateController.Truncate(rule.DbType, rule.Size, v);
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
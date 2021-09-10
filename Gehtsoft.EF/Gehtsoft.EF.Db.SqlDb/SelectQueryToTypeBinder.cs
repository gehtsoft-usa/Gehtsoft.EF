using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The binding rule for the select query binder.
    /// </summary>
    [DocgenIgnore]
    [Obsolete("Use SelectQueryResultBinder instead")]
    [ExcludeFromCodeCoverage]
    public class SelectQueryTypeBinderRule
    {
        /// <summary>
        /// The flag indicating that the column does not exist in the result set.
        /// </summary>
        internal bool ColumnDoesNotExist { get; set; } = false;
        /// <summary>
        /// The index of the column.
        /// </summary>
        public int ColumnIndex { get; set; }
        /// <summary>
        /// The column name.
        /// </summary>
        public string ColumnName { get; set; }
        /// <summary>
        /// The property name.
        /// </summary>
        public string PropertyName { get; set; }
        /// <summary>
        /// The select query binder to which the rule belongs to.
        /// </summary>
        public SelectQueryTypeBinder Binder { get; set; }
        /// <summary>
        /// The property value accessor.
        /// </summary>
        public IPropertyAccessor PropertyInfo { get; set; }
        /// <summary>
        /// The flag indicating that the column is a primary key.
        /// </summary>
        public bool PrimaryKey { get; set; }

        internal SelectQueryTypeBinderRule(string column, string property, IPropertyAccessor propertyInfo = null)
        {
            ColumnIndex = -1;
            ColumnName = column;
            PropertyName = property;
            PropertyInfo = propertyInfo;
        }

        internal SelectQueryTypeBinderRule(int column, string property, IPropertyAccessor propertyInfo = null)
        {
            ColumnIndex = column;
            ColumnName = null;
            PropertyName = property;
            PropertyInfo = propertyInfo;
        }

        internal SelectQueryTypeBinderRule(SelectQueryTypeBinder binder, string property, IPropertyAccessor propertyInfo = null)
        {
            ColumnIndex = -1;
            ColumnName = null;
            Binder = binder;
            PropertyName = property;
            PropertyInfo = propertyInfo;
        }
    }

    /// <summary>
    /// The binder of a select resultset into object(s).
    /// </summary>
    [DocgenIgnore]
    [Obsolete("Use SelectQueryResultBinder instead")]
    [ExcludeFromCodeCoverage]
    public class SelectQueryTypeBinder
    {
        private readonly Type mType;
        private readonly List<SelectQueryTypeBinderRule> mRules = new List<SelectQueryTypeBinderRule>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">The type of the object to bind the resultset</param>
        public SelectQueryTypeBinder(Type type)
        {
            mType = type;
        }

        /// <summary>
        /// Adds binding of a column by the name to a property by the property name for a simple type.
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="property"></param>
        /// <param name="pk"></param>
        public void AddBinding(string columnName, string property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnName, property) { PrimaryKey = pk });
        }

        /// <summary>
        /// Adds binding of a column by the name to a property by the property accessor for a simple type.
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="property"></param>
        /// <param name="pk"></param>
        public void AddBinding(string columnName, IPropertyAccessor property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnName, property.Name, property) { PrimaryKey = pk });
        }

        /// <summary>
        /// Adds binding of a column by the index to the property by the name for a simple type.
        /// </summary>
        /// <param name="columnIndex"></param>
        /// <param name="property"></param>
        /// <param name="pk"></param>
        public void AddBinding(int columnIndex, string property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnIndex, property) { PrimaryKey = pk });
        }

        /// <summary>
        /// Adds binding of a column by the index to the property by the accessor for a simple type.
        /// </summary>
        /// <param name="columnIndex"></param>
        /// <param name="property"></param>
        /// <param name="pk"></param>
        public void AddBinding(int columnIndex, IPropertyAccessor property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnIndex, property.Name, property) { PrimaryKey = pk });
        }

        /// <summary>
        /// Adds binding by the property name for a complex type using another binder.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="property"></param>
        public void AddBinding(SelectQueryTypeBinder binder, string property)
        {
            mRules.Add(new SelectQueryTypeBinderRule(binder, property));
        }

        /// <summary>
        /// Adds binding by the property accessor for a complex type using another binder.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="property"></param>
        public void AddBinding(SelectQueryTypeBinder binder, IPropertyAccessor property)
        {
            mRules.Add(new SelectQueryTypeBinderRule(binder, property.Name, property));
        }

        /// <summary>
        /// Creates binding automatically.
        ///
        /// Auto-binding:
        /// * does not recognizes and does not create binding of a complex types automatically
        /// * assumes that the names of the properties equals to the name of the columns in resulset.
        /// </summary>
        /// <param name="prefix"></param>
        public void AutoBind(string prefix = null)
        {
            foreach (PropertyInfo propertyInfo in mType.GetProperties())
                mRules.Add(new SelectQueryTypeBinderRule(prefix == null ? propertyInfo.Name : prefix + propertyInfo.Name, propertyInfo.Name, new PropertyAccessor(propertyInfo)) { PrimaryKey = false });
        }

        /// <summary>
        /// Reads one object from the query.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public object Read(IDbQuery query)
        {
            if (!query.CanRead)
                return null;

            if (mRules.Count == 0)
                AutoBind();

            object r = Activator.CreateInstance(mType);

            foreach (SelectQueryTypeBinderRule rule in mRules)
            {
                if (rule.ColumnDoesNotExist)
                    continue;

                if (rule.PropertyInfo == null)
                {
                    rule.PropertyInfo = new PropertyAccessor(mType.GetProperty(rule.PropertyName));
                    if (rule.PropertyInfo == null)
                        throw new EfSqlException(EfExceptionCode.PropertyNotFound, rule.PropertyName);
                }

                if (rule.Binder != null)
                {
                    rule.PropertyInfo.SetValue(r, rule.Binder.Read(query));
                }
                else
                {
                    if (rule.ColumnIndex == -1)
                    {
                        rule.ColumnIndex = query.FindField(rule.ColumnName, true);
                        if (rule.ColumnIndex == -1)
                        {
                            rule.ColumnDoesNotExist = true;
                            continue;
                        }
                    }

                    if (rule.ColumnIndex == -1)
                        continue;

                    if (rule.PrimaryKey && query.IsNull(rule.ColumnIndex))
                        return null;
                    rule.PropertyInfo.SetValue(r, query.GetValue(rule.ColumnIndex, rule.PropertyInfo.PropertyType));
                }
            }
            return r;
        }

        /// <summary>
        /// Writes the current row into a dynamic object.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="expandoObject"></param>
        /// <returns></returns>
        internal bool BindToDynamic(IDbQuery query, ExpandoObject expandoObject)
        {
            if (mRules.Count == 0)
                AutoBind();

            IDictionary<string, object> dict = (IDictionary<string, object>)expandoObject;

            foreach (SelectQueryTypeBinderRule rule in mRules)
            {
                if (rule.ColumnDoesNotExist)
                    continue;

                if (rule.PropertyInfo == null)
                {
                    rule.PropertyInfo = new PropertyAccessor(mType.GetProperty(rule.PropertyName));
                    if (rule.PropertyInfo == null)
                        throw new EfSqlException(EfExceptionCode.PropertyNotFound, rule.PropertyName);
                }

                if (rule.Binder != null)
                {
                    dict.Add(rule.PropertyInfo.Name, rule.Binder.Read(query));
                }
                else
                {
                    if (rule.ColumnIndex == -1)
                        rule.ColumnIndex = query.FindField(rule.ColumnName, true);
                    if (rule.ColumnIndex == -1)
                    {
                        rule.ColumnDoesNotExist = true;
                        continue;
                    }
                    if (rule.PrimaryKey && query.IsNull(rule.ColumnIndex))
                        return false;
                    dict.Add(rule.PropertyInfo.Name, query.GetValue(rule.ColumnIndex, rule.PropertyInfo.PropertyType));
                }
            }
            return true;
        }

        /// <summary>
        /// Checks whether the column is bound to the entity
        /// </summary>
        /// <param name="index"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public bool BindsColumn(int index, IDbQuery query)
        {
            foreach (SelectQueryTypeBinderRule rule in mRules)
            {
                if (rule.Binder != null)
                {
                    if (rule.Binder.BindsColumn(index, query))
                        return true;
                }
                else
                {
                    if (rule.ColumnIndex == -1)
                        rule.ColumnIndex = query.FindField(rule.ColumnName);
                    if (rule.ColumnIndex == index)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Reads one object from the query resultset (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public T Read<T>(IDbQuery query) where T : class
        {
            return Read(query) as T;
        }
    }
}

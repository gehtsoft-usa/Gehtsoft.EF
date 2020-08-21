using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb
{
    public class SelectQueryTypeBinderRule
    {
        public int ColumnIndex { get; set; }
        public string ColumnName { get; set; }
        public string PropertyName { get; set; }
        public SelectQueryTypeBinder Binder { get; set; }
        public IPropertyAccessor PropertyInfo { get; set; }
        public bool PrimaryKey { get; set; }

        public SelectQueryTypeBinderRule(string column, string property, IPropertyAccessor propertyInfo = null)
        {
            ColumnIndex = -1;
            ColumnName = column;
            PropertyName = property;
            PropertyInfo = propertyInfo;
        }

        public SelectQueryTypeBinderRule(int column, string property, IPropertyAccessor propertyInfo = null)
        {
            ColumnIndex = column;
            ColumnName = null;
            PropertyName = property;
            PropertyInfo = propertyInfo;
        }

        public SelectQueryTypeBinderRule(SelectQueryTypeBinder binder, string property, IPropertyAccessor propertyInfo = null)
        {
            ColumnIndex = -1;
            ColumnName = null;
            Binder = binder;
            PropertyName = property;
            PropertyInfo = propertyInfo;
        }
    }

    public class SelectQueryTypeBinder
    {
        private Type mType;
        private List<SelectQueryTypeBinderRule> mRules = new List<SelectQueryTypeBinderRule>();

        public SelectQueryTypeBinder(Type type)
        {
            mType = type;
        }
    
        public void AddBinding(string columnName, string property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnName, property) {PrimaryKey = pk});
        }

        public void AddBinding(string columnName, IPropertyAccessor property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnName, property.Name, property) { PrimaryKey = pk });
        }

        public void AddBinding(int columnIndex, string property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnIndex, property) { PrimaryKey = pk });
        }

        public void AddBinding(int columnIndex, IPropertyAccessor property, bool pk = false)
        {
            mRules.Add(new SelectQueryTypeBinderRule(columnIndex, property.Name, property) { PrimaryKey = pk });
        }

        public void AddBinding(SelectQueryTypeBinder binder, string property)
        {
            mRules.Add(new SelectQueryTypeBinderRule(binder, property));
        }

        public void AddBinding(SelectQueryTypeBinder binder, IPropertyAccessor property)
        {
            mRules.Add(new SelectQueryTypeBinderRule(binder, property.Name, property));
        }

        public void AutoBind(string prefix = null)
        {
            foreach (PropertyInfo propertyInfo in mType.GetProperties())
                mRules.Add(new SelectQueryTypeBinderRule((prefix == null ? propertyInfo.Name : prefix + propertyInfo.Name), propertyInfo.Name, new PropertyAccessor(propertyInfo)) {PrimaryKey = false});
        }

        public object Read(IDbQuery query)
        {
            if (!query.CanRead)
                return null;

            if (mRules.Count == 0)
                AutoBind();

            object r = Activator.CreateInstance(mType);

            foreach (SelectQueryTypeBinderRule rule in mRules)
            {
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
                        rule.ColumnIndex = query.FindField(rule.ColumnName);
                    if (rule.PrimaryKey && query.IsNull(rule.ColumnIndex))
                        return null;
                    rule.PropertyInfo.SetValue(r, query.GetValue(rule.ColumnIndex, rule.PropertyInfo.PropertyType));
                }
            }
            return r;
        }

        public bool BindToDynamic(IDbQuery query, ExpandoObject expandoObject)
        {
            if (mRules.Count == 0)
                AutoBind();

            IDictionary<string, object> dict = (IDictionary<string, object>) expandoObject;

            foreach (SelectQueryTypeBinderRule rule in mRules)
            {
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
                        rule.ColumnIndex = query.FindField(rule.ColumnName);
                    if (rule.PrimaryKey && query.IsNull(rule.ColumnIndex))
                        return false;
                    dict.Add(rule.PropertyInfo.Name, query.GetValue(rule.ColumnIndex, rule.PropertyInfo.PropertyType));
                }
            }
            return true;
        }

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

        public T Read<T>(IDbQuery query) where T : class
        {
            return Read(query) as T;
        }
    }
}

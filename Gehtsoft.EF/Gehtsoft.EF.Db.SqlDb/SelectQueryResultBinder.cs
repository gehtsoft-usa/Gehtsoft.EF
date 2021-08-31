using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb
{
    internal class SelectQueryResultBinderRule
    {
        public bool ColumnDoesNotExist { get; set; }
        public int ColumnIndex { get; set; }
        public string ColumnName { get; }
        public IPropertyAccessor PropertyAccessor { get; }
        public bool IsPrimaryKey { get; }

        public SelectQueryResultBinderRule(int columnIndex, string columnName, IPropertyAccessor propertyAccessor, bool isPrimaryKey)
        {
            ColumnIndex = columnIndex;
            ColumnName = columnName;
            PropertyAccessor = propertyAccessor;
            IsPrimaryKey = isPrimaryKey;
        }
    }
    internal class SelectQuerySubBinderRule
    {
        public SelectQueryResultBinder Binder { get; }
        public IPropertyAccessor PropertyAccessor { get; }

        public SelectQuerySubBinderRule(SelectQueryResultBinder binder, IPropertyAccessor propertyAccessor)
        {
            Binder = binder;
            PropertyAccessor = propertyAccessor;
        }
    }

    internal class ExpandoPropertyAccessor : IPropertyAccessor
    {
        public string Name { get; }

        public Type PropertyType { get; }

        public Attribute GetCustomAttribute(Type attributeType) => null;

        public ExpandoPropertyAccessor(string name, Type propertyType = null)
        {
            Name = name;
            PropertyType = propertyType ?? typeof(object);
        }

        public object GetValue(object thisObject)
        {
            if (((IDictionary<string, object>)thisObject).TryGetValue(Name, out object v))
                return v;
            return null;
        }

        public void SetValue(object thisObject, object value)
        {
            if (value != null)
                ((IDictionary<string, object>)thisObject)[Name] = value;
            else
                ((IDictionary<string, object>)thisObject).Remove(Name);
        }
    }

    public static class PropertyAcessorFactory
    {
        public static IPropertyAccessor Create(Type type, string name, bool exact = true)
        {
            if (type == typeof(ExpandoObject))
                return new ExpandoPropertyAccessor(name);

            var entity = AllEntities.Get(type, false);

            if (entity != null)
            {
                TableDescriptor.ColumnInfo columnInfo = null;

                if (exact)
                    columnInfo = entity.TableDescriptor.HasColumn(name) ? entity[name] : null;
                else
                    columnInfo = ((IEnumerable<TableDescriptor.ColumnInfo>)entity)
                        .FirstOrDefault(ci => ci.ID.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                              ci.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (columnInfo == null)
                    return null;

                return columnInfo.PropertyAccessor;
            }
            else
            {
                PropertyInfo propertyInfo = null;

                if (exact)
                    propertyInfo = type.GetProperty(name);
                else
                    propertyInfo = Array.Find(type.GetProperties(), p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (propertyInfo == null)
                    return null;

                return new PropertyAccessor(propertyInfo);
            }
        }
    }

    /// <summary>
    /// The binder for the select results
    /// </summary>
    public class SelectQueryResultBinder
    {
        private readonly Type mTargetType;
        private readonly List<SelectQueryResultBinderRule> mRules = new List<SelectQueryResultBinderRule>();
        private readonly List<SelectQuerySubBinderRule> mSubBinders = new List<SelectQuerySubBinderRule>();

        internal IReadOnlyList<SelectQueryResultBinderRule> Rules => mRules;
        internal IReadOnlyList<SelectQuerySubBinderRule> SubBinders => mSubBinders;

        internal void AddBinding(int columnIndex, string columnName, IPropertyAccessor accessor, bool isPrimaryKey)
        {
            if (columnIndex < 0 && string.IsNullOrEmpty(columnName))
                throw new ArgumentException("Either column name or column index must be set", nameof(columnIndex));

            if (accessor == null)
                throw new ArgumentNullException(nameof(accessor), "The property is not found in the target type");

            mRules.Add(new SelectQueryResultBinderRule(columnIndex, columnName, accessor, isPrimaryKey));
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="targetType"></param>
        public SelectQueryResultBinder(Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            mTargetType = targetType;
        }

        /// <summary>
        /// Adds a binding by column index manually.
        /// </summary>
        /// <param name="columnIndex"></param>
        /// <param name="propertyName"></param>
        /// <param name="isPrimaryKey"></param>
        public void AddBinding(int columnIndex, string propertyName, bool isPrimaryKey = false)
            => AddBinding(columnIndex, null, PropertyAcessorFactory.Create(mTargetType, propertyName), isPrimaryKey);

        /// <summary>
        /// Adds a binding by the column name manually
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="propertyName"></param>
        /// <param name="isPrimaryKey"></param>
        public void AddBinding(string columnName, string propertyName, bool isPrimaryKey = false)
            => AddBinding(-1, columnName, PropertyAcessorFactory.Create(mTargetType, propertyName), isPrimaryKey);

        /// <summary>
        /// Adds binding of a property which consists of another type.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="propertyName"></param>
        public void AddBinding(SelectQueryResultBinder binder, string propertyName)
            => AddBinding(binder, PropertyAcessorFactory.Create(mTargetType, propertyName));

        /// <summary>
        /// Adds binding of a property which consists of another type.
        /// </summary>
        /// <param name="binder"></param>
        /// <param name="propertyAccessor"></param>
        internal void AddBinding(SelectQueryResultBinder binder, IPropertyAccessor propertyAccessor)
        {
            if (binder == null)
                throw new ArgumentNullException(nameof(binder));

            if (propertyAccessor == null)
                throw new ArgumentNullException(nameof(propertyAccessor), "Check whether the expected property exists in the target type");

            mSubBinders.Add(new SelectQuerySubBinderRule(binder, propertyAccessor));
        }

        /// <summary>
        /// Automatically binds the query
        /// </summary>
        /// <param name="query"></param>
        /// <param name="prefix"></param>
        public void AutoBindQuery(SqlDbQuery query, string prefix = null)
        {
            if (mRules.Count != 0)
                throw new InvalidOperationException("The binder is already bound");

            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (!query.CanRead)
                throw new ArgumentException("The query must be a successfully executed SELECT query", nameof(query));

            for (int i = 0; i < query.FieldCount; i++)
            {
                var field = query.Field(i);
                var name = field.Name;
                if (!string.IsNullOrEmpty(prefix) && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(prefix.Length);

                var accessor = PropertyAcessorFactory.Create(mTargetType, name, false);

                if (accessor != null)
                    mRules.Add(new SelectQueryResultBinderRule(i, field.Name, accessor, false));
            }
        }

        /// <summary>
        /// Automatically binds all type's properties.
        ///
        /// For entities, the bind will seek for the field names associated with the entity property
        ///
        /// For types, the bind will use the naming policy specified
        /// <param name="prefix">The prefix for the column names in the query</param>
        /// <param name="policy">The naming policy used to convert the property names to the fields.</param>
        /// </summary>
        public void AutoBindType(string prefix = null, EntityNamingPolicy? policy = null)
        {
            if (mTargetType == typeof(ExpandoObject))
                throw new InvalidOperationException("Can't auto-bind dynamic object, auto-bind the query instead");

            if (policy == null)
                policy = EntityNamingPolicy.AsIs;

            var e = AllEntities.Get(mTargetType, false);
            if (e == null)
            {
                //it is not an entity
                var properties = mTargetType.GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];

                    if (property.GetMethod == null ||
                        property.SetMethod == null ||
                        property.IsSpecialName ||
                        property.GetMethod.IsStatic)
                        continue;

                    var name = EntityNameConvertor.ConvertName(property.Name, policy);

                    if (!string.IsNullOrEmpty(prefix))
                        name = prefix + name;

                    AddBinding(-1, name, new PropertyAccessor(property), false);
                }
            }
            else
            {
                //it is an entity
                for (int i = 0; i < e.TableDescriptor.Count; i++)
                {
                    var ci = e.TableDescriptor[i];

                    var name = ci.Name;

                    if (!string.IsNullOrEmpty(prefix))
                        name = prefix + name;

                    AddBinding(-1, name, ci.PropertyAccessor, ci.PrimaryKey);
                }
            }
        }

        /// <summary>
        /// Binds the current row of the query to an object
        /// </summary>
        /// <param name="query"></param>
        /// <param name="toBind"></param>
        /// <returns></returns>
        public bool Read(SqlDbQuery query, object toBind)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));
            if (toBind == null)
                throw new ArgumentNullException(nameof(toBind));
            if (!query.CanRead)
                throw new ArgumentException("The query must be a successful SELECT query.", nameof(query));

            IDictionary<string, object> dict = toBind as IDictionary<string, object>;
            if (toBind.GetType() != mTargetType && dict == null)
                throw new ArgumentException("The object should have the same type as set when the binder is created", nameof(toBind));

            for (int i = 0; i < mRules.Count; i++)
            {
                var rule = mRules[i];
                if (rule.ColumnDoesNotExist)
                    continue;
                if (rule.ColumnIndex < 0)
                {
                    rule.ColumnIndex = query.FindField(rule.ColumnName, true);
                    if (rule.ColumnIndex < 0)
                    {
                        rule.ColumnDoesNotExist = true;
                        continue;
                    }
                }

                object r;

                if (query.IsNull(rule.ColumnIndex))
                    r = null;
                else
                    r = query.GetValue(rule.ColumnIndex, rule.PropertyAccessor.PropertyType);

                if (r == null && rule.IsPrimaryKey)
                    return false;

                Assign(rule.PropertyAccessor, r);
            }

            for (int i = 0; i < mSubBinders.Count; i++)
            {
                var rule = mSubBinders[i];
                var r = rule.Binder.Read(query);
                Assign(rule.PropertyAccessor, r);
            }

            return true;

            void Assign(IPropertyAccessor accessor, object r)
            {
                if (dict != null)
                    dict[accessor.Name] = r;
                else
                    accessor.SetValue(toBind, r);
            }
        }

        /// <summary>
        /// Reads the current row the query to the object.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public object Read(SqlDbQuery query)
        {
            var o = Activator.CreateInstance(mTargetType);
            if (!Read(query, o))
                return null;
            return o;
        }
    }

    public static class SelectQueryResultBinderExension
    {
        public static T Read<T>(this SelectQueryResultBinder binder, SqlDbQuery query) where T : new()
        {
            var t = new T();
            binder.Read(query, t);
            return t;
        }

        public static dynamic ReadToDynamic(this SelectQueryResultBinder binder, SqlDbQuery query)
        {
            var t = new ExpandoObject();
            binder.Read(query, t);
            return t;
        }
    }
}

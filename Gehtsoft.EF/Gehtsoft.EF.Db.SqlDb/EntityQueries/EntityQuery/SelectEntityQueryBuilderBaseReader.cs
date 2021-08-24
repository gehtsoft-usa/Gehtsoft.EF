using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntityQueryReader<T>
        where T : class
    {
        private readonly SelectEntitiesQueryBase mQuery;
        private bool mBound = false;

        public SelectEntityQueryReader(SelectEntitiesQueryBase query)
        {
            mQuery = query;
        }

        internal class PropertyTarget
        {
            public PropertyInfo Property { get; set; }
            public int Column { get; set; }
        }

        private readonly List<PropertyTarget> mPropertyTargets = new List<PropertyTarget>();
        private readonly List<Action<T, SelectEntitiesQueryBase>> mActions = new List<Action<T, SelectEntitiesQueryBase>>();

        protected void Bind()
        {
            Type targetType = typeof(T);

            for (int column = 0; column < mQuery.SelectEntityBuilder.SelectQueryBuilder.Resultset.Count; column++)
            {
                var item = mQuery.SelectEntityBuilder.SelectQueryBuilder.Resultset[column];

                if (!string.IsNullOrEmpty(item.Alias))
                {
                    PropertyInfo propertyInfo = targetType.GetProperty(item.Alias, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (propertyInfo != null && propertyInfo.CanWrite)
                        mPropertyTargets.Add(new PropertyTarget() { Property = propertyInfo, Column = column });
                }
            }
            mBound = true;
        }

        public void Bind(string propertyName, string columnName) => Bind(propertyName, FindColumn(columnName));

        public void Bind(string propertyName, int columnIndex)
        {
            Type targetType = typeof(T);
            PropertyInfo propertyInfo = targetType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                mPropertyTargets.Add(new PropertyTarget() { Property = propertyInfo, Column = columnIndex });
                return;
            }

            throw new ArgumentException($"A property {propertyName} that has setter is not found", nameof(propertyName));
        }

        public void Bind<TV>(Expression<Func<T, TV>> expression, string columnName) => Bind(PropertyOfParameterInfo(expression)?.Name, columnName);

        public void Bind<TV>(Expression<Func<T, TV>> expression, int columnIndex) => Bind(PropertyOfParameterInfo(expression)?.Name, columnIndex);

        public void Bind(Action<T, SelectEntitiesQueryBase> action) => mActions.Add(action);

        public void Bind<TV>(Expression<Func<T, TV>> target, Func<SelectEntitiesQueryBase, TV> source)
        {
            PropertyInfo info = PropertyOfParameterInfo(target);
            Bind((obj, query) => info.SetValue(obj, source(query)));
        }

        public void Scan(Func<T, bool> action)
        {
            while (true)
            {
                T t = ReadOne();
                if (t == null)
                    return;
                if (!action(t))
                    return;
            }
        }

        public void ScanAll(Action<T> action)
            => Scan(t =>
                {
                    action(t);
                    return true;
                });

        public T ReadOne()
        {
            if (!mBound)
                Bind();

            if (!mQuery.ReadNext())
                return null;

            T t = Activator.CreateInstance<T>();
            for (int i = 0; i < mPropertyTargets.Count; i++)
            {
                var target = mPropertyTargets[i];
                target.Property.SetValue(t, mQuery.GetValue(target.Column, target.Property.PropertyType));
            }

            for (int i = 0; i < mActions.Count; i++)
                mActions[i](t, mQuery);

            return t;
        }

        public TC ReadAll<TC>()
            where TC : IList<T>, new()
        {
            TC rc = new TC();
            T t;
            while (true)
            {
                t = ReadOne();
                if (t == null)
                    break;
                rc.Add(t);
            }
            return rc;
        }

        public EntityCollection<T> ReadAll() => ReadAll<EntityCollection<T>>();

        protected static PropertyInfo PropertyOfParameterInfo(Expression expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.NodeType == ExpressionType.Lambda)
                return PropertyOfParameterInfo(((LambdaExpression)expression).Body);

            if (expression.NodeType == ExpressionType.Convert)
                return PropertyOfParameterInfo(((UnaryExpression)expression).Operand);

            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression e = (MemberExpression)expression;
                MemberInfo mi = e.Member;
                if (mi.MemberType == MemberTypes.Property && e.Expression.NodeType == ExpressionType.Parameter)
                    return (PropertyInfo)mi;
            }

            throw new ArgumentException("Expression is not a plain property access expression", nameof(expression));
        }

        protected int FindColumn(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            for (int column = 0; column < mQuery.SelectEntityBuilder.SelectQueryBuilder.Resultset.Count; column++)
            {
                var item = mQuery.SelectEntityBuilder.SelectQueryBuilder.Resultset[column];
                if (item.Alias == name)
                    return column;
            }

            throw new ArgumentException($"Column {name} isn't found in the resultset", nameof(name));
        }
    }
}
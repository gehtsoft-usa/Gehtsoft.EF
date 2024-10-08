﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// The provider for LINQ queryable entities.
    /// </summary>
    public class QueryableEntityProvider : IQueryProvider
    {
        protected readonly ISqlDbConnectionFactory mConnectionProvider;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionProvider"></param>
        public QueryableEntityProvider(ISqlDbConnectionFactory connectionProvider)
        {
            mConnectionProvider = connectionProvider;
        }

        public QueryableEntity<T> Entities<T>() => new QueryableEntity<T>(this);

        public IQueryable CreateQuery(Expression expression)
        {
            Type elementType = GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(QueryableEntity<>).MakeGenericType(elementType), this, expression);
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        [DocgenIgnore]
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new QueryableEntity<TElement>(this, expression);
        }

        [DocgenIgnore]
        public object Execute(Expression expression)
        {
            return Execute(expression, false);
        }

        [DocgenIgnore]
        public TResult Execute<TResult>(Expression expression)
        {
            bool isEnumerable = false;
            Type tr = typeof(TResult);
            if (tr.IsGenericType && tr.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                isEnumerable = true;
            }
            else
            {
                foreach (var iface in tr.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        isEnumerable = true;
                        break;
                    }
                }
            }

            object r = Execute(expression, isEnumerable);

            if (r != null && typeof(TResult).IsValueType)
            {
                if (r.GetType() != typeof(TResult))
                    r = Convert.ChangeType(r, typeof(TResult));
            }
            return (TResult)r;
        }

        internal static Type GetElementType(Type seqType)
        {
            Type ienum = FindIEnumerable(seqType);
            if (ienum == null) return seqType;
            return ienum.GetTypeInfo().GetGenericArguments()[0];
        }

        private static Type FindIEnumerable(Type seqType)
        {
            if (seqType == null || seqType == typeof(string))
                return null;

            if (seqType.IsArray)
                return typeof(IEnumerable<>).MakeGenericType(seqType.GetElementType());

            if (seqType.GetTypeInfo().IsGenericType)
            {
                foreach (Type arg in seqType.GetGenericArguments())
                {
                    Type ienum = typeof(IEnumerable<>).MakeGenericType(arg);
                    if (ienum.IsAssignableFrom(seqType))
                    {
                        return ienum;
                    }
                }
            }

            Type[] ifaces = seqType.GetInterfaces();
            if (ifaces != null && ifaces.Length > 0)
            {
                foreach (Type iface in ifaces)
                {
                    Type ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }

            if (seqType.GetTypeInfo().BaseType != null && seqType.GetTypeInfo().BaseType != typeof(object))
            {
                return FindIEnumerable(seqType.GetTypeInfo().BaseType);
            }

            return null;
        }

        internal class CompiledQuery : IDisposable
        {
            internal SelectEntitiesQueryBase Query { get; set; }
            internal SelectEntitiesQuery EntityQuery { get; set; }
            internal Type EntityType { get; set; }
            internal Type ReturnType { get; set; }   

            internal Func<SelectEntitiesQueryBase, Type, object> ReadRow;

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                Query?.Dispose();
                Query = EntityQuery = null;
            }

            ~CompiledQuery()
            {
                Dispose(false);
            }
        }

        internal CompiledQuery CompileToQuery<T>(SqlDbConnection connection, Expression<Action<QueryableEntity<T>>> query)
            => CompileToQuery(connection, query.Body);

        internal CompiledQuery CompileToQuery(SqlDbConnection connection, Expression expression)
        {
            SelectExpressionCompiler compiler = new SelectExpressionCompiler();
            compiler.Compile(expression);

            if (compiler.EntityType == null)
                throw new ArgumentException("The entity type isn't found", nameof(expression));

            CompiledQuery query = new CompiledQuery();

            if (compiler.Select == null)
            {
                query.Query = query.EntityQuery = connection.GetSelectEntitiesQuery(compiler.EntityType);
                query.EntityType = compiler.EntityType;
                query.ReturnType = null;
            }
            else
            {
                query.Query = connection.GetGenericSelectEntityQuery(compiler.EntityType);
                query.Query.AddWholeTree();
                query.EntityQuery = null;
                query.EntityType = compiler.EntityType;
                query.ReturnType = compiler.Select.ReturnType;
            }

            if (compiler.HasOrderBy)
            {
                foreach (Expression subExpression in compiler.OrderBy)
                    query.Query.AddOrderBy(subExpression, SortDir.Asc);
            }

            Tuple<string, Expression>[] grouping = compiler.HasGroupBy ? compiler.GroupByKey.ToArray() : null;
            Type groupByKeyType = compiler.GroupByKeyType;
            Type groupingType = compiler.GroupingType;
            Type entityEnumerationType1 = typeof(IEnumerable<>).MakeGenericType(compiler.EntityType);
            Type entityEnumerationType2 = typeof(IQueryable<>).MakeGenericType(compiler.EntityType);
            Type entityEnumerationType3 = typeof(EntityCollection<>).MakeGenericType(compiler.EntityType);
            Type entityEnumerationType4 = typeof(QueryableEntity<>).MakeGenericType(compiler.EntityType);

            if (grouping != null)
            {
                foreach (Tuple<string, Expression> keyValue in grouping)
                    query.Query.AddGroupBy(keyValue.Item2);
            }

            if (compiler.Skip != null)
                query.Query.Skip = (int)compiler.Skip;

            if (compiler.Take != null)
                query.Query.Limit = (int)compiler.Take;

            if (compiler.Select != null)
            {
                if (compiler.Select.Body.NodeType == ExpressionType.New)
                {
                    //create a custom result set
                    NewExpression newExpression = (NewExpression)compiler.Select.Body;
                    for (int i = 0; i < newExpression.Members.Count; i++)
                    {
                        if (groupByKeyType != null && newExpression.Arguments[i].NodeType == ExpressionType.MemberAccess)
                        {
                            MemberExpression memberExpression = (MemberExpression)newExpression.Arguments[i];
                            if (grouping?.Length == 1 && memberExpression.Type == groupByKeyType)
                            {
                                query.Query.AddToResultset(grouping[0].Item2, newExpression.Members[i].Name);
                                continue;
                            }
                            else if (memberExpression.Expression.Type == groupByKeyType)
                            {
                                if (grouping != null)
                                {
                                    foreach (var v in grouping)
                                    {
                                        if (v.Item1 == memberExpression.Member.Name)
                                        {
                                            query.Query.AddToResultset(v.Item2, newExpression.Members[i].Name);
                                            break;
                                        }
                                    }
                                }
                                continue;
                            }
                        }
                        else if (newExpression.Arguments[i].NodeType == ExpressionType.Call)
                        {
                            MethodCallExpression callExpression = (MethodCallExpression)newExpression.Arguments[i];

                            if (callExpression.Arguments.Count > 0 &&
                                (callExpression.Arguments[0].Type == groupingType ||
                                 callExpression.Arguments[0].Type == entityEnumerationType1 ||
                                 callExpression.Arguments[0].Type == entityEnumerationType2 ||
                                 callExpression.Arguments[0].Type == entityEnumerationType3 ||
                                 callExpression.Arguments[0].Type == entityEnumerationType4))
                            {
                                MethodInfo method = null;
                                Expression argument = null;

                                if (callExpression.Method.Name == "Sum")
                                {
                                    method = typeof(SqlFunction).GetTypeInfo().GetMethod(nameof(SqlFunction.Sum));
                                    method = method.MakeGenericMethod(newExpression.Arguments[i].Type);
                                    argument = ExtractBody(callExpression.Arguments[1]);
                                }
                                else if (callExpression.Method.Name == "Min")
                                {
                                    method = typeof(SqlFunction).GetTypeInfo().GetMethod(nameof(SqlFunction.Min));
                                    method = method.MakeGenericMethod(newExpression.Arguments[i].Type);
                                    argument = ExtractBody(callExpression.Arguments[1]);
                                }
                                else if (callExpression.Method.Name == "Max")
                                {
                                    method = typeof(SqlFunction).GetTypeInfo().GetMethod(nameof(SqlFunction.Max));
                                    method = method.MakeGenericMethod(newExpression.Arguments[i].Type);
                                    argument = ExtractBody(callExpression.Arguments[1]);
                                }
                                else if (callExpression.Method.Name == "Count")
                                {
                                    method = typeof(SqlFunction).GetTypeInfo().GetMethod(nameof(SqlFunction.Count));
                                    if (callExpression.Arguments.Count > 1)
                                    {
                                        var l = callExpression.Arguments[1] as LambdaExpression;
                                        if (l != null)
                                            compiler.Where.Add(l.Body);
                                    }
                                    argument = null;
                                }
                                else if (callExpression.Method.Name == "Average")
                                {
                                    method = typeof(SqlFunction).GetTypeInfo().GetMethod(nameof(SqlFunction.Avg));
                                    method = method.MakeGenericMethod(callExpression.Arguments[1].Type.GetGenericArguments()[1]);
                                    argument = ExtractBody(callExpression.Arguments[1]);
                                }

                                if (method != null)
                                {
                                    MethodCallExpression newCallExpression = argument == null ? Expression.Call(method) : Expression.Call(method, argument);
                                    LambdaExpression lambaExpression = Expression.Lambda((Expression)newCallExpression, Expression.Parameter(compiler.EntityType), Expression.Parameter(newExpression.Arguments[i].Type));
                                    query.Query.AddToResultset(lambaExpression.Body, newExpression.Members[i].Name);
                                    continue;
                                }
                            }                           
                        }

                        query.Query.AddToResultset(newExpression.Arguments[i], newExpression.Members[i].Name);
                    }

                    query.ReadRow = CreateType;
                }
                else
                {
                    //select one value
                    query.Query.AddToResultset(compiler.Select.Body, "value");
                    query.ReadRow = ReadOneValue;
                }
            }

            if (compiler.HasWhere)
            {
                foreach (Expression subExpression in compiler.Where)
                    query.Query.Where.Add(LogOp.And, subExpression);
            }

            return query;
        }

        private static Expression ExtractBody(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Quote)
                expression = ((UnaryExpression)expression).Operand;
            if (expression.NodeType != ExpressionType.Lambda)
                throw new ArgumentException("Only lambda function is supported as parameter", nameof(expression));
            return ((LambdaExpression)expression).Body;
        }

        private static object ReadOneValue(SelectEntitiesQueryBase query, Type type)
        {
            return query.GetValue(0, type);
        }

        private static object CreateType(SelectEntitiesQueryBase query, Type type)
        {
            int cc = query.FieldCount;
            if (query.ResultsetSize < cc)
                cc = query.ResultsetSize;
            object[] args = new object[cc];
            for (int i = 0; i < cc; i++)
            {
                SelectQueryBuilderResultsetItem column = query.ResultColumn(i);
                PropertyInfo propertyInfo = type.GetProperty(column.Alias);
                args[i] = query.GetValue(i, propertyInfo.PropertyType);
            }
            object returnValue = Activator.CreateInstance(type, args);
            return returnValue;
        }

        protected object Execute(Expression expression, bool isEnumerable)
        {
            SqlDbConnection connection = null;
            try
            {
                connection = mConnectionProvider.GetConnection();

                using (CompiledQuery compiledQuery = CompileToQuery(connection, expression))
                {
                    if (compiledQuery.EntityQuery != null)
                    {
                        if (isEnumerable)
                            return compiledQuery.EntityQuery.GetAllAsEnumerable(compiledQuery.EntityType);
                        else
                            return compiledQuery.EntityQuery.ReadOne();
                    }
                    else
                    {
                        compiledQuery.Query.Execute();

                        if (!isEnumerable)
                        {
                            if (compiledQuery.Query.ReadNext())
                                return compiledQuery.ReadRow(compiledQuery.Query, compiledQuery.ReturnType);
                            else
                                return null;
                        }

                        object result = Activator.CreateInstance(typeof(List<>).MakeGenericType(compiledQuery.ReturnType));
                        MethodInfo add = result.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);

                        if (add == null)
                            throw new InvalidOperationException("List<> does not have Add method anymore");

                        object[] args = new object[1];
                        while (compiledQuery.Query.ReadNext())
                        {
                            args[0] = compiledQuery.ReadRow(compiledQuery.Query, compiledQuery.ReturnType);
                            add.Invoke(result, args);
                        }

                        return result;
                    }
                }
            }
            finally
            {
                if (mConnectionProvider.NeedDispose)
                    connection?.Dispose();
            }
        }

        /// <summary>
        /// Inserts the object into the database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="o"></param>
        public void Insert<T>(object o)
        {
            SqlDbConnection connection = null;
            try
            {
                connection = mConnectionProvider.GetConnection();
                using (ModifyEntityQuery query = connection.GetInsertEntityQuery<T>())
                    query.Execute(o);
            }
            finally
            {
                if (mConnectionProvider.NeedDispose)
                    connection?.Dispose();
            }
        }

        /// <summary>
        /// Updates the object in the database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="o"></param>
        public void Update<T>(object o)
        {
            SqlDbConnection connection = null;
            try
            {
                connection = mConnectionProvider.GetConnection();
                using (ModifyEntityQuery query = connection.GetUpdateEntityQuery<T>())
                    query.Execute(o);
            }
            finally
            {
                if (mConnectionProvider.NeedDispose)
                    connection?.Dispose();
            }
        }

        /// <summary>
        /// Deletes the object from the database.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="o"></param>
        public void Delete<T>(object o)
        {
            SqlDbConnection connection = null;
            try
            {
                connection = mConnectionProvider.GetConnection();
                using (ModifyEntityQuery query = connection.GetDeleteEntityQuery<T>())
                    query.Execute(o);
            }
            finally
            {
                if (mConnectionProvider.NeedDispose)
                    connection?.Dispose();
            }
        }
    }
}
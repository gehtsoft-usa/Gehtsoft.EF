using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// Extensions to use LINQ expressions in queries.
    /// </summary>
    public static class EntityQueryLinqExtension
    {
        internal static void BindExpressionParameters(this ConditionEntityQueryBase query, ExpressionCompiler.Result result)
        {
            foreach (var param in result.Params)
            {
                object value;
                if (param.Value is Expression expression)
                {
                    if (param.CompiledExpression == null)
                        param.CompiledExpression = System.Linq.Expressions.Expression.Lambda(expression).Compile();
                    value = param.CompiledExpression.DynamicInvoke();
                }
                else
                {
                    value = param.Value;
                }

                if (value is SelectEntitiesQueryBase)
                    query.CopyParametersFrom(value as SelectEntitiesQueryBase);
                else
                    query.BindParam(param.Name, ParameterDirection.Input, value, value.GetType());
            }
        }

        internal static void AddToResultset(this SelectEntitiesQueryBase query, Expression expression, string alias)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.AddExpressionToResultset(result.Expression.ToString(), result.HasAggregates, DbType.Object, expression.Type, alias);
            query.BindExpressionParameters(result);
        }

        /// <summary>
        /// Adds expression that involves one entity to the resultset.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TRes"></typeparam>
        /// <param name="query"></param>
        /// <param name="expression"></param>
        /// <param name="alias"></param>
        public static void AddToResultset<T, TRes>(this SelectEntitiesQueryBase query, Expression<Func<T, TRes>> expression, string alias = null) => AddToResultset(query, expression.Body, alias);

        /// <summary>
        /// Adds expression that involves two entities to the resultset.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="TRes"></typeparam>
        /// <param name="query"></param>
        /// <param name="expression"></param>
        /// <param name="alias"></param>
        public static void AddToResultset<T, T1, TRes>(this SelectEntitiesQueryBase query, Expression<Func<T, T1, TRes>> expression, string alias = null) => AddToResultset(query, expression.Body, alias);

        internal static void AddOrderBy(this SelectEntitiesQueryBase query, Expression expression, SortDir direction)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.AddOrderByExpr(result.Expression.ToString(), direction);
        }

        /// <summary>
        /// Adds expression to the order by
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="expression"></param>
        /// <param name="direction"></param>
        public static void AddOrderBy<T>(this SelectEntitiesQueryBase query, Expression<Func<T, object>> expression, SortDir direction = SortDir.Asc) => AddOrderBy(query, expression.Body, direction);

        internal static void AddGroupBy(this SelectEntitiesQueryBase query, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.AddGroupByExpr(result.Expression.ToString());
        }

        /// <summary>
        /// Adds expression to the group by
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="expression"></param>
        public static void AddGroupBy<T>(this SelectEntitiesQueryBase query, Expression<Func<T, object>> expression) => AddGroupBy(query, expression.Body);

        internal static void AddEntity(this SelectEntitiesQueryBase query, Type type, TableJoinType joinType, Expression joinExpression)
        {
            var x = query.AddEntity(type, TableJoinType.None);
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(joinExpression);
            x.JoinType = joinType;
            x.On.Add(LogOp.And, result.Expression.ToString());
            query.BindExpressionParameters(result);
        }

        /// <summary>
        /// Adds entity to the query using the specified expression to join
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="query"></param>
        /// <param name="type"></param>
        /// <param name="joinType"></param>
        /// <param name="joinExpression"></param>
        public static void AddEntity<T1, T2>(this SelectEntitiesQueryBase query, Type type, TableJoinType joinType, Expression<Func<T1, T2, bool>> joinExpression) => AddEntity(query, type, joinType, joinExpression.Body);

        /// <summary>
        /// Adds entity to the query using the specified expression that connects new entity to two other entities.
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="query"></param>
        /// <param name="type"></param>
        /// <param name="joinType"></param>
        /// <param name="joinExpression"></param>
        public static void AddEntity<T1, T2, T3>(this SelectEntitiesQueryBase query, Type type, TableJoinType joinType, Expression<Func<T1, T2, T3, bool>> joinExpression) => AddEntity(query, type, joinType, joinExpression.Body);

        /// <summary>
        /// Add the value to set to all the records for the specified property.
        /// </summary>
        /// <typeparam name="TE"></typeparam>
        /// <typeparam name="TR"></typeparam>
        /// <param name="query"></param>
        /// <param name="propertyName"></param>
        /// <param name="expression"></param>
        public static void AddUpdateColumn<TE, TR>(this MultiUpdateEntityQuery query, string propertyName, Expression<Func<TE, TR>> expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.AddUpdateColumnByExpression(propertyName, result.Expression.ToString());
            query.BindExpressionParameters(result);
        }
    }

    /// <summary>
    /// Extensions to use LINQ expressions in query conditions.
    /// </summary>
    public static class EntityQueryConditionLinqExtension
    {
        internal static EntityQueryConditionBuilder Add(this EntityQueryConditionBuilder builder, LogOp op, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(builder.BaseQuery);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            builder.Add(op, result.Expression.ToString());
            builder.BaseQuery.BindExpressionParameters(result);
            return builder;
        }

        /// <summary>
        /// Add the expression to a condition and connect it using And.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="expression"></param>
        public static EntityQueryConditionBuilder Expression<T>(this EntityQueryConditionBuilder builder, Expression<Func<T, bool>> expression) => builder.Add(LogOp.And, expression.Body);

        /// <summary>
        /// Add the expression, that uses two entities, to a query condition and connect it using And.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <param name="builder"></param>
        /// <param name="expression"></param>
        public static EntityQueryConditionBuilder Expression<T, T1>(this EntityQueryConditionBuilder builder, Expression<Func<T, T1, bool>> expression) => builder.Add(LogOp.And, expression.Body);

        /// <summary>
        /// Add the expression and connect it using the specified logical operator.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="logOp"></param>
        /// <param name="expression"></param>
        public static EntityQueryConditionBuilder Expression<T>(this EntityQueryConditionBuilder builder, LogOp logOp, Expression<Func<T, bool>> expression) => builder.Add(logOp, expression.Body);

        internal static SingleEntityQueryConditionBuilder Expression(this SingleEntityQueryConditionBuilder builder, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(builder.Builder.BaseQuery);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            builder.Raw(result.Expression.ToString());
            builder.Builder.BaseQuery.BindExpressionParameters(result);
            return builder;
        }

        /// <summary>
        /// Sets a condition to the expression.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Expression<T>(this SingleEntityQueryConditionBuilder builder, Expression<Func<T, bool>> expression) => builder.Expression(expression.Body);

        /// <summary>
        /// Sets a condition to the expression that uses two entities.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <param name="builder"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Expression<T, T1>(this SingleEntityQueryConditionBuilder builder, Expression<Func<T, T1, bool>> expression) => builder.Expression(expression.Body);
    }

    [ExcludeFromCodeCoverage]
    [DocgenIgnore]
    public static class EntityQueryLinqExtensionBackwardCompatibility
    {
        [Obsolete("Use Where property of the query instead")]
        internal static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.Where.Add(logOp, result.Expression.ToString());
            query.BindExpressionParameters(result);
        }

        [Obsolete("Use Where property of the query instead")]
        public static void AddWhereFilter<T>(this ConditionEntityQueryBase query, Expression<Func<T, bool>> expression) => query.AddWhereFilter(LogOp.And, expression.Body);

        [Obsolete("Use Where property of the query instead")]
        public static void AddWhereFilter<T, T1>(this ConditionEntityQueryBase query, Expression<Func<T, T1, bool>> expression) => query.AddWhereFilter(LogOp.And, expression.Body);

        [Obsolete("Use Where property of the query instead")]
        public static void AddWhereFilter<T>(this ConditionEntityQueryBase query, LogOp logOp, Expression<Func<T, bool>> expression) => query.AddWhereFilter(logOp, expression.Body);

        [Obsolete("Use Where property of the query instead")]
        public static void AddWhereFilter<T, T1>(this SelectEntitiesQueryBase query, LogOp logOp, Expression<Func<T, T1, bool>> expression) => query.AddWhereFilter(logOp, expression.Body);
    }
}

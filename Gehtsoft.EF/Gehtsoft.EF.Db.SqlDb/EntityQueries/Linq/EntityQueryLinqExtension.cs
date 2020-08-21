using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    public static class EntityQueryLinqExtension
    {
        internal static void BindExpressionToWhere(this ConditionEntityQueryBase query, ExpressionCompiler.Result result)
        {
            foreach (var param in result.Params)
            {
                object value;
                if (param.Value is Expression)
                {
                    if (param.CompiledExpression == null)
                        param.CompiledExpression = System.Linq.Expressions.Expression.Lambda((Expression) (param.Value)).Compile();
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
            query.BindExpressionToWhere(result);
        }

        public static void AddToResultset<T, TRes>(this SelectEntitiesQueryBase query, Expression<Func<T, TRes>> expression, string alias = null) => AddToResultset(query, expression.Body, alias);
        
        public static void AddToResultset<T, T1, TRes>(this SelectEntitiesQueryBase query, Expression<Func<T, T1, TRes>> expression, string alias = null) => AddToResultset(query, expression.Body, alias);

        internal static void AddOrderBy(this SelectEntitiesQueryBase query, Expression expression, SortDir direction)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.AddOrderByExpr(result.Expression.ToString(), direction);
        }


        public static void AddOrderBy<T>(this SelectEntitiesQueryBase query, Expression<Func<T, object>> expression, SortDir direction = SortDir.Asc) => AddOrderBy(query, expression.Body, direction);

        internal static void AddGroupBy(this SelectEntitiesQueryBase query, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.AddGroupByExpr(result.Expression.ToString());
        }

        public static void AddGroupBy<T>(this SelectEntitiesQueryBase query, Expression<Func<T, object>> expression) => AddGroupBy(query, expression.Body);

        internal static void AddEntity(this SelectEntitiesQueryBase query, Type type, TableJoinType joinType, Expression joinExpression)
        {
            var x = query.AddEntity(type, TableJoinType.None);
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(joinExpression);
            x.JoinType = joinType;
            x.On.Add(LogOp.And, result.Expression.ToString());
            query.BindExpressionToWhere(result);
        }

        public static void AddEntity<T1, T2>(this SelectEntitiesQueryBase query, Type type, TableJoinType joinType, Expression<Func<T1, T2, bool>> joinExpression) => AddEntity(query, type, joinType, joinExpression.Body);

        public static void AddEntity<T1, T2, T3>(this SelectEntitiesQueryBase query, Type type, TableJoinType joinType, Expression<Func<T1, T2, T3, bool>> joinExpression) => AddEntity(query, type, joinType, joinExpression.Body);

        internal static void Add(this EntityQueryConditionBuilder builder, LogOp op, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(builder.BaseQuery);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            builder.Add(op, result.Expression.ToString());
            builder.BaseQuery.BindExpressionToWhere(result);
        }

        public static void Expression<T>(this EntityQueryConditionBuilder builder, Expression<Func<T, bool>> expression) => builder.Add(LogOp.And, expression.Body);
        
        public static void Expression<T, T1>(this EntityQueryConditionBuilder builder, Expression<Func<T, T1, bool>> expression) => builder.Add(LogOp.And, expression.Body);

        public static void Expression<T>(this EntityQueryConditionBuilder builder, LogOp logOp, Expression<Func<T, bool>> expression) => builder.Add(logOp, expression.Body);


        internal static void Expression(this SingleEntityQueryConditionBuilder builder, LogOp op, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(builder.Builder.BaseQuery);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            builder.Builder.Add(op, result.Expression.ToString());
            builder.Builder.BaseQuery.BindExpressionToWhere(result);
        }

        public static void Expression<T>(this SingleEntityQueryConditionBuilder builder, Expression<Func<T, bool>> expression) => builder.Expression(LogOp.And, expression.Body);
        
        public static void Expression<T, T1>(this SingleEntityQueryConditionBuilder builder, Expression<Func<T, T1, bool>> expression) => builder.Expression(LogOp.And, expression.Body);

        public static void Expression<T>(this SingleEntityQueryConditionBuilder builder, LogOp logOp, Expression<Func<T, bool>> expression) => builder.Expression(logOp, expression.Body);
        
        public static void Expression<T, T1>(this SingleEntityQueryConditionBuilder builder, LogOp logOp, Expression<Func<T, T1, bool>> expression) => builder.Expression(logOp, expression.Body);
    }



    public static class EntityQueryLinqExtensionBackwardCompatibility
    {
        [Obsolete("Use Where property of the query instead")]
        internal static void AddWhereFilter(this ConditionEntityQueryBase query, LogOp logOp, Expression expression)
        {
            ExpressionCompiler compiler = new ExpressionCompiler(query);
            ExpressionCompiler.Result result = compiler.Visit(expression);
            query.Where.Add(logOp, result.Expression.ToString());
            query.BindExpressionToWhere(result);
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

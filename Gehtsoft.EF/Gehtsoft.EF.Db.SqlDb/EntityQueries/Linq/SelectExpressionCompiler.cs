using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    public class SelectExpressionCompiler
    {
        public Type EntityType { get; private set; }

        public LambdaExpression Select { get; private set; }

        private List<Expression> mWhere;
        public bool HasWhere => mWhere?.Count > 0;
        public IEnumerable<Expression> Where => mWhere;

        private List<Expression> mOrderBy;

        public bool HasOrderBy => mOrderBy?.Count > 0;

        public IEnumerable<Expression> OrderBy => mOrderBy;

        public int? Skip { get; private set; }

        public int? Take { get; private set; }

        private List<Tuple<string, Expression>> mGroupByKey;
        private Type mGroupByKeyType;
        private Type mGroupingType;

        public IReadOnlyCollection<Tuple<string, Expression>> GroupByKey => mGroupByKey;
        public Type GroupByKeyType => mGroupByKeyType;
        public Type GroupingType => mGroupingType;
        public bool HasGroupBy => mGroupByKey?.Count > 0;

        public SelectExpressionCompiler()
        {
        }

        public void Compile(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Constant && expression.Type.IsConstructedGenericType)
            {
                Type candidate = EntityType = expression.Type.GenericTypeArguments[0];
                Type iface = typeof(QueryableEntity<>).MakeGenericType(candidate);
                if (iface.IsAssignableFrom(expression.Type))
                {
                    EntityType = candidate;
                    if (mGroupByKeyType != null)
                        mGroupingType = typeof(IGrouping<,>).MakeGenericType(mGroupByKeyType, EntityType);
                }
            }
            else if (expression.NodeType == ExpressionType.Call)
            {
                MethodCallExpression callExpression = (MethodCallExpression)expression;
                if (callExpression.Method.Name == "Where")
                {
                    if (mWhere == null)
                        mWhere = new List<Expression>();
                    mWhere.Add(callExpression.Arguments[1]);
                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "OrderBy")
                {
                    if (mOrderBy == null)
                        mOrderBy = new List<Expression>();

                    LambdaExpression orderByLambda;
                    Expression orderByExpression = callExpression.Arguments[1];

                    if (orderByExpression.NodeType == ExpressionType.Quote)
                        orderByExpression = ((UnaryExpression)orderByExpression).Operand;

                    if (orderByExpression.NodeType == ExpressionType.Lambda)
                        orderByLambda = (LambdaExpression)orderByExpression;
                    else
                        throw new ArgumentException("Only lambda functions are supported in order by", nameof(expression));

                    if (orderByLambda.Body.NodeType == ExpressionType.MemberAccess)
                    {
                        mOrderBy.Add(callExpression.Arguments[1]);
                    }
                    else if (orderByLambda.Body.NodeType == ExpressionType.New)
                    {
                        NewExpression newExpression = (NewExpression)orderByLambda.Body;
                        for (int i = 0; i < newExpression.Members.Count; i++)
                        {
                            if (newExpression.Arguments[i].NodeType == ExpressionType.MemberAccess)
                                mOrderBy.Add(newExpression.Arguments[i]);
                        }
                    }
                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "GroupBy")
                {
                    if (mGroupByKey == null)
                        mGroupByKey = new List<Tuple<string, Expression>>();

                    Expression groupByExpression = callExpression.Arguments[1];
                    LambdaExpression groupByLambda;

                    if (groupByExpression.NodeType == ExpressionType.Quote)
                        groupByExpression = ((UnaryExpression)groupByExpression).Operand;

                    if (groupByExpression.NodeType == ExpressionType.Lambda)
                        groupByLambda = (LambdaExpression)groupByExpression;
                    else
                        throw new ArgumentException("Only lambda functions are supported in group by", nameof(expression));

                    mGroupByKeyType = groupByLambda.ReturnType;

                    if (groupByLambda.Body.NodeType == ExpressionType.MemberAccess)
                    {
                        mGroupByKey.Add(new Tuple<string, Expression>("Key", groupByLambda.Body));
                    }
                    else if (groupByLambda.Body.NodeType == ExpressionType.New)
                    {
                        NewExpression newExpression = (NewExpression)groupByLambda.Body;
                        for (int i = 0; i < newExpression.Members.Count; i++)
                        {
                            if (newExpression.Arguments[i].NodeType == ExpressionType.MemberAccess)
                                mGroupByKey.Add(new Tuple<string, Expression>(newExpression.Members[i].Name, newExpression.Arguments[i]));
                            else
                                throw new ArgumentException("Only member access is supported in group by key");
                        }
                    }
                    else if (groupByLambda.Body.NodeType == ExpressionType.Constant)
                    {
                        //omit group by
                    }
                    else
                        throw new ArgumentException("Only member access is supported in group by key");

                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "Select")
                {
                    Expression selectExpression = callExpression.Arguments[1];
                    if (selectExpression.NodeType == ExpressionType.Quote)
                        selectExpression = ((UnaryExpression)selectExpression).Operand;
                    if (selectExpression.NodeType == ExpressionType.Lambda)
                        Select = (LambdaExpression)selectExpression;
                    else
                        throw new ArgumentException("Only lambda functions are supported in select", nameof(expression));
                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "Count")
                {
                    Select = Expression.Lambda(Expression.Call(typeof(SqlFunction).GetMethod("Count")));
                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "Max" || callExpression.Method.Name == "Min" || callExpression.Method.Name == "Average" || callExpression.Method.Name == "Sum")
                {
                    string name = callExpression.Method.Name;
                    if (name == "Average")
                        name = "Avg";
                    MethodInfo methodInfo = typeof(SqlFunction).GetMethod(name);
                    if (methodInfo.IsGenericMethodDefinition)
                        methodInfo = methodInfo.MakeGenericMethod(callExpression.Method.ReturnType);
                    Expression argument = callExpression.Arguments[1];
                    if (argument.NodeType == ExpressionType.Quote)
                        argument = (argument as UnaryExpression).Operand;
                    if (argument.NodeType != ExpressionType.Lambda)
                        throw new ArgumentException("Only lambda is supported as argument for max function", nameof(expression));
                    LambdaExpression lambdaArgument = argument as LambdaExpression;

                    Select = Expression.Lambda(Expression.Call(methodInfo, lambdaArgument.Body));
                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "Skip")
                {
                    Expression valueExpression = callExpression.Arguments[1];
                    Skip = (int)(Expression.Lambda(valueExpression).Compile().DynamicInvoke());
                    Compile(callExpression.Arguments[0]);
                }
                else if (callExpression.Method.Name == "Take")
                {
                    Expression valueExpression = callExpression.Arguments[1];
                    Take = (int)(Expression.Lambda(valueExpression).Compile().DynamicInvoke());
                    Compile(callExpression.Arguments[0]);
                }
                else
                {
                    throw new ArgumentException($"The method {callExpression.Method.Name} isn't supported", nameof(expression));
                }
            }
        }
    }
}
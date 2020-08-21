using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    internal class ExpressionCompiler
    {
        internal class ExpressionParameter
        {
            internal string Name { get; set; }
            internal object Value { get; set; }
            internal Delegate CompiledExpression { get; set; }
        }

        internal class ForcedTypeInfo
        {
            internal Type EntityType { get; set; }
            internal QueryBuilderEntity QueryBuilderEntity { get; set; }
        }

        private List<ForcedTypeInfo> mForcedTypes = null;

        internal void AddForcedType(QueryBuilderEntity entity)
        {
            if (mForcedTypes == null)
                mForcedTypes = new List<ForcedTypeInfo>();

            mForcedTypes.Add(new ForcedTypeInfo() { EntityType = entity.EntityType, QueryBuilderEntity = entity });
        }

        internal class Result
        {
            public StringBuilder Expression { get; } = new StringBuilder();
            public List<ExpressionParameter> Params { get; set; } = new List<ExpressionParameter>();
            public bool IsParameterExpression { get; private set; }
            public bool HasAggregates { get; set; }

            public Result()
            {
                IsParameterExpression = false;
            }

            public Result(SqlDbLanguageSpecifics specifics, string name, object value)
            {
                IsParameterExpression = true;
                Expression.Append($"{specifics.ParameterInQueryPrefix}{name}");
                Params.Add(new ExpressionParameter() { Name = $"{specifics.ParameterPrefix}{name}", Value = value });
            }

            public void Add(Result otherResult)
            {
                Params.AddRange(otherResult.Params);
                Expression.Append(otherResult.Expression);
                IsParameterExpression = false;
            }
        }

        private static int gLeqParam = 0;
        protected static int NextLeqParam => gLeqParam = (gLeqParam + 1) & 0xff_ffff;

        private readonly ConditionEntityQueryBase mQuery;
        private readonly SqlDbLanguageSpecifics mSpecifics;

        public ExpressionCompiler(ConditionEntityQueryBase query)
        {
            mQuery = query;
            mSpecifics = mQuery.Query.Connection.GetLanguageSpecifics();
        }

        public void ThrowExpressionType(Type type, string parameterName) => throw new ArgumentException($"Unsupported LINQ expression class {type.Name}", parameterName);

        public void ThrowExpressionOperation(ExpressionType type, string parameterName) => throw new ArgumentException($"Unsupported LINQ expression type {type}", parameterName);

        public void ThrowUnknowPropertyOfType(Type type, string property, string parameterName) => throw new ArgumentException($"Unknown property {property} of {type.Name}", parameterName);

        public void ThrowQueryArgument() => throw new ArgumentException("The operation requires a sub query as an argument", "subquery");

        public void ThrowReferenceArgument() => throw new ArgumentException("The operation requires a reference as an argument", "reference");

        public Result Visit(Expression node)
        {
            if (node is ConstantExpression)
            {
                return new Result(mSpecifics, "leq" + NextLeqParam, ((ConstantExpression)node).Value);
            }
            else if (node is UnaryExpression)
            {
                UnaryExpression unaryExpression = (UnaryExpression)node;
                if (unaryExpression.NodeType == ExpressionType.Quote)
                {
                    LambdaExpression expression = (LambdaExpression)unaryExpression.Operand;
                    return Visit(expression.Body);
                }
                else
                {
                    Result arg = Visit(((UnaryExpression)node).Operand);
                    if (arg.IsParameterExpression)
                    {
                        arg.Params[0].Value = node;
                        return arg;
                    }
                    else
                    {
                        return ProcessUnary((UnaryExpression)node, arg);
                    }
                }
            }
            else if (node is MemberExpression)
            {
                EntityQueryWithWhereBuilder.EntityQueryItem queryPath = IsQueryPath((MemberExpression)(node));
                Result res = new Result();
                if (queryPath != null)
                {
                    ConditionEntityQueryBase.InQueryName n = mQuery.GetReference(queryPath);
                    res.Expression.Append($"{n.Alias}");
                    return res;
                }
                else
                {
                    return new Result(mSpecifics, "leq" + NextLeqParam, node);
                }
            }
            else if (node is BinaryExpression)
            {
                Result left = Visit(((BinaryExpression)node).Left);
                Result right = Visit(((BinaryExpression)node).Right);
                if (left.IsParameterExpression && right.IsParameterExpression)
                {
                    left.Params[0].Value = node;
                    return left;
                }
                else
                {
                    return ProcessBinary((BinaryExpression)node, left, right);
                }
            }
            else if (node is MethodCallExpression)
            {
                return ProcessCall((MethodCallExpression)node);
            }
            else if (node.NodeType == ExpressionType.Parameter)
            {
                ParameterExpression param = (ParameterExpression)node;
                Type parameterType = param.Type;
                EntityQueryWithWhereBuilder.EntityQueryItem r = mQuery.GetItem(parameterType, AllEntities.Inst[parameterType].PrimaryKey.ID, 0);
                if (r == null)
                    ThrowExpressionType(node.Type, nameof(node));
                Result res = new Result();
                res.Expression.Append($"{r.QueryEntity.Alias}.{r.Column.Name}");
                return res;
            }
            else
            {
                ThrowExpressionType(node.Type, nameof(node));
                return null;
            }
        }

        private Result ProcessUnary(UnaryExpression node, Result arg)
        {
            Result result = new Result();
            result.Expression.Append("(");
            switch (node.NodeType)
            {
                case ExpressionType.Convert:
                    if (node.Type == typeof(int) || node.Type == typeof(short) || node.Type == typeof(long))
                    {
                        result.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToInteger, new string[] { arg.Expression.ToString() }));
                        result.Params.AddRange(arg.Params);
                    }
                    else if (node.Type == typeof(double) || node.Type == typeof(float))
                    {
                        result.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToDouble, new string[] { arg.Expression.ToString() }));
                        result.Params.AddRange(arg.Params);
                    }
                    else if (node.Type == typeof(DateTime))
                    {
                        result.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToDate, new string[] { arg.Expression.ToString() }));
                        result.Params.AddRange(arg.Params);
                    }
                    else if (node.Type == typeof(string))
                    {
                        result.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToString, new string[] { arg.Expression.ToString() }));
                    }
                    else
                    {
                        return arg;
                    }
                    break;

                case ExpressionType.UnaryPlus:
                    result.Expression.Append("+(");
                    result.Add(arg);
                    result.Expression.Append(")");
                    break;

                case ExpressionType.Negate:
                    result.Expression.Append("-(");
                    result.Add(arg);
                    result.Expression.Append(")");
                    break;

                case ExpressionType.Not:
                    result.Expression.Append("NOT (");
                    result.Add(arg);
                    result.Expression.Append(")");
                    break;
            }
            result.Expression.Append(")");
            result.HasAggregates = arg.HasAggregates;
            return result;
        }

        private Result ProcessBinary(BinaryExpression node, Result left, Result right)
        {
            Result result = new Result();
            result.Expression.Append("(");
            result.HasAggregates = left.HasAggregates | right.HasAggregates;
            result.Add(left);

            string op;
            bool suppressRight = false;

            switch (node.NodeType)
            {
                case ExpressionType.Add:
                    op = "+";
                    break;

                case ExpressionType.Subtract:
                    op = "-";
                    break;

                case ExpressionType.Multiply:
                    op = "*";
                    break;

                case ExpressionType.Divide:
                    op = "/";
                    break;

                case ExpressionType.And:
                    op = " AND ";
                    break;

                case ExpressionType.AndAlso:
                    op = " AND ";
                    break;

                case ExpressionType.Or:
                    op = " OR ";
                    break;

                case ExpressionType.OrElse:
                    op = " OR ";
                    break;

                case ExpressionType.Equal:
                    if ((node.Right is ConstantExpression expression1) && expression1.Value == null)
                    {
                        op = " IS NULL ";
                        suppressRight = true;
                    }
                    else
                    {
                        op = "=";
                    }

                    break;

                case ExpressionType.NotEqual:
                    if ((node.Right is ConstantExpression expression2) && expression2.Value == null)
                    {
                        op = " IS NOT NULL ";
                        suppressRight = true;
                    }
                    else
                    {
                        op = "<>";
                    }

                    break;

                case ExpressionType.GreaterThan:
                    op = ">";
                    break;

                case ExpressionType.GreaterThanOrEqual:
                    op = ">=";
                    break;

                case ExpressionType.LessThan:
                    op = "<";
                    break;

                case ExpressionType.LessThanOrEqual:
                    op = "<=";
                    break;

                default:
                    ThrowExpressionOperation(node.NodeType, nameof(node));
                    return null;
            }

            result.Expression.Append(op);
            if (!suppressRight)
                result.Add(right);

            result.Expression.Append(")");
            return result;
        }

        private EntityQueryWithWhereBuilder.EntityQueryItem IsQueryPath(MemberExpression node)
        {
            if (node.Expression != null && node.NodeType == ExpressionType.MemberAccess && node.Member.MemberType == MemberTypes.Property)
            {
                if (node.Expression.NodeType == ExpressionType.Parameter)
                {
                    ParameterExpression leftSide = (ParameterExpression)node.Expression;
                    Type parameterType = leftSide.Type;
                    EntityQueryWithWhereBuilder.EntityQueryItem r = mQuery.GetItem(parameterType, node.Member.Name);
                    return r;
                }
                else if (node.Expression.NodeType == ExpressionType.MemberAccess)
                {
                    EntityQueryWithWhereBuilder.EntityQueryItem rleft = IsQueryPath((MemberExpression)node.Expression);
                    if (rleft == null)
                        return null;

                    EntityQueryWithWhereBuilder.EntityQueryItem res = mQuery.GetItem($"{rleft.Path}.{node.Member.Name}");
                    if (res == null)
                        throw new ArgumentException("The property does not exists. Check whether the related type is added to the query", nameof(node));
                    return res;
                }
                else if (node.Expression.NodeType == ExpressionType.ArrayIndex)
                {
                    BinaryExpression arrayExpression = (BinaryExpression)node.Expression;
                    if (arrayExpression.Left.NodeType == ExpressionType.Parameter)
                    {
                        Type parameterType = arrayExpression.Left.Type.GetElementType();
                        int occurrence = (int)Expression.Lambda(arrayExpression.Right).Compile().DynamicInvoke();
                        EntityQueryWithWhereBuilder.EntityQueryItem r = mQuery.GetItem(parameterType, node.Member.Name, occurrence);
                        return r;
                    }
                }
                else if (node.Expression.Type == typeof(ConditionEntityQueryBase.InQueryName))
                {
                    object value = Expression.Lambda(node).Compile().DynamicInvoke();
                    return (((ConditionEntityQueryBase.InQueryName)value).Item);
                }
                else
                    return null;
            }
            return null;
        }

        private Result ProcessCall(MethodCallExpression callNode)
        {
            Result res = new Result();

            IReadOnlyCollection<Expression> argumentExpressions = callNode.Arguments;

            Result[] argumentResults = new Result[(argumentExpressions?.Count ?? 0) + (callNode.Object == null ? 0 : 1)];
            bool allParams = true;
            res.HasAggregates = false;
            if (argumentExpressions != null)
            {
                int i = 0;
                if (callNode.Object != null)
                {
                    argumentResults[i] = Visit(callNode.Object);
                    allParams &= argumentResults[i].IsParameterExpression;
                    res.HasAggregates |= argumentResults[i].HasAggregates;
                    i++;
                }

                foreach (Expression arg in argumentExpressions)
                {
                    argumentResults[i] = Visit(arg);
                    allParams &= argumentResults[i].IsParameterExpression;
                    res.HasAggregates |= argumentResults[i].HasAggregates;
                    i++;
                }
            }

            if (callNode.Method.DeclaringType == typeof(SqlFunction))
            {
                if (callNode.Method.Name == nameof(SqlFunction.Count))
                {
                    res.HasAggregates = true;
                    res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Count, null));
                    return res;
                }
                else if (callNode.Method.Name == nameof(SqlFunction.Value))
                {
                    if (!(argumentResults[0].Params[0].Value is Expression))
                    {
                        ThrowReferenceArgument();
                        return null;
                    }

                    if (typeof(ConditionEntityQueryBase.InQueryName).IsAssignableFrom((argumentResults[0].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[0].Params[0].Value as Expression).Compile().DynamicInvoke();
                        ConditionEntityQueryBase.InQueryName queryPath = value as ConditionEntityQueryBase.InQueryName;
                        res.Expression.Append(queryPath.Alias);
                        return res;
                    }
                    else if (typeof(SelectEntitiesQueryBase).IsAssignableFrom((argumentResults[0].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[0].Params[0].Value as Expression).Compile().DynamicInvoke();
                        SelectEntitiesQueryBase query = value as SelectEntitiesQueryBase;
                        query.SelectBuilder.PrepareQuery();
                        res.Expression.Append($"({query.SelectBuilder.Query})");
                        res.Params.Add(new ExpressionParameter() { Value = query });
                        return res;
                    }
                    else
                    {
                        ThrowReferenceArgument();
                        return null;
                    }
                }
                else if (callNode.Method.Name == nameof(SqlFunction.In))
                {
                    if (!(argumentResults[1].Params[0].Value is Expression))
                    {
                        ThrowQueryArgument();
                        return null;
                    }

                    if (typeof(SelectEntitiesQueryBase).IsAssignableFrom((argumentResults[1].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[1].Params[0].Value as Expression).Compile().DynamicInvoke();
                        SelectEntitiesQueryBase query = value as SelectEntitiesQueryBase;
                        query.SelectBuilder.PrepareQuery();
                        res.Expression.Append($" {argumentResults[0].Expression.ToString()} IN ({query.SelectBuilder.Query})");
                        res.Params.Add(new ExpressionParameter() { Value = query });
                        return res;
                    }
                    else if (typeof(AQueryBuilder).IsAssignableFrom((argumentResults[1].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[1].Params[0].Value as Expression).Compile().DynamicInvoke();
                        AQueryBuilder query = value as AQueryBuilder;
                        query.PrepareQuery();
                        res.Expression.Append($" {argumentResults[0].Expression.ToString()} IN ({query.Query})");
                        return res;
                    }
                    else
                    {
                        ThrowQueryArgument();
                        return null;
                    }
                }
                else if (callNode.Method.Name == nameof(SqlFunction.NotIn))
                {
                    if (!(argumentResults[1].Params[0].Value is Expression))
                    {
                        ThrowQueryArgument();
                        return null;
                    }

                    if (typeof(SelectEntitiesQueryBase).IsAssignableFrom((argumentResults[1].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[1].Params[0].Value as Expression).Compile().DynamicInvoke();
                        SelectEntitiesQueryBase query = value as SelectEntitiesQueryBase;
                        query.SelectBuilder.PrepareQuery();
                        res.Expression.Append($" {argumentResults[0].Expression.ToString()} NOT IN ({query.SelectBuilder.Query})");
                        res.Params.Add(new ExpressionParameter() { Value = query });
                        return res;
                    }
                    else if (typeof(AQueryBuilder).IsAssignableFrom((argumentResults[1].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[1].Params[0].Value as Expression).Compile().DynamicInvoke();
                        AQueryBuilder query = value as AQueryBuilder;
                        query.PrepareQuery();
                        res.Expression.Append($" {argumentResults[0].Expression.ToString()} NOT IN ({query.Query})");
                        return res;
                    }
                    else
                    {
                        ThrowQueryArgument();
                        return null;
                    }
                }
                else if (callNode.Method.Name == nameof(SqlFunction.Exists))
                {
                    if (!(argumentResults[0].Params[0].Value is Expression))
                    {
                        ThrowQueryArgument();
                        return null;
                    }

                    if (typeof(SelectEntitiesQueryBase).IsAssignableFrom((argumentResults[0].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[0].Params[0].Value as Expression).Compile().DynamicInvoke();
                        SelectEntitiesQueryBase query = value as SelectEntitiesQueryBase;
                        query.SelectBuilder.PrepareQuery();
                        res.Expression.Append($" EXISTS ({query.SelectBuilder.Query})");
                        res.Params.Add(new ExpressionParameter() { Value = query });
                        return res;
                    }
                    else if (typeof(AQueryBuilder).IsAssignableFrom((argumentResults[0].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[0].Params[0].Value as Expression).Compile().DynamicInvoke();
                        AQueryBuilder query = value as AQueryBuilder;
                        query.PrepareQuery();
                        res.Expression.Append($" EXISTS ({query.Query})");
                        return res;
                    }
                    else
                    {
                        ThrowQueryArgument();
                        return null;
                    }
                }
                else if (callNode.Method.Name == nameof(SqlFunction.NotExists))
                {
                    if (!(argumentResults[0].Params[0].Value is Expression))
                    {
                        ThrowQueryArgument();
                        return null;
                    }

                    if (typeof(SelectEntitiesQueryBase).IsAssignableFrom((argumentResults[0].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[0].Params[0].Value as Expression).Compile().DynamicInvoke();
                        SelectEntitiesQueryBase query = value as SelectEntitiesQueryBase;
                        query.SelectBuilder.PrepareQuery();
                        res.Expression.Append($" NOT EXISTS ({query.SelectBuilder.Query})");
                        res.Params.Add(new ExpressionParameter() { Value = query });
                        return res;
                    }
                    else if (typeof(AQueryBuilder).IsAssignableFrom((argumentResults[0].Params[0].Value as Expression).Type))
                    {
                        object value = Expression.Lambda(argumentResults[0].Params[0].Value as Expression).Compile().DynamicInvoke();
                        AQueryBuilder query = value as AQueryBuilder;
                        query.PrepareQuery();
                        res.Expression.Append($" NOT EXISTS ({query.Query})");
                        return res;
                    }
                    else
                    {
                        ThrowQueryArgument();
                        return null;
                    }
                }
            }

            if (allParams)
                return new Result(mSpecifics, "leq" + NextLeqParam, callNode);

            bool isFunction = (callNode.Method.DeclaringType == typeof(SqlFunction));

            string[] stringResults = new string[argumentResults.Length + (callNode.Object == null ? 0 : 1)];

            for (int i = 0; i < argumentResults.Length; i++)
            {
                stringResults[i] = argumentResults[i].Expression.ToString();
                res.Params.AddRange(argumentResults[i].Params);
            }

            if (isFunction && callNode.Method.Name == nameof(SqlFunction.Sum))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Sum, stringResults));
                res.HasAggregates = true;
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Min))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Min, stringResults));
                res.HasAggregates = true;
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Max))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Max, stringResults));
                res.HasAggregates = true;
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Avg))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Avg, stringResults));
                res.HasAggregates = true;
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Like))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Like, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Lower))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Lower, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Upper))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Upper, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Trim))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Trim, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Concat))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Concat, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.TrimLeft))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.TrimLeft, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.TrimRight))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.TrimRight, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.ToString))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToString, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.ToDate))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToDate, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.ToDouble))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToDouble, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.ToInteger))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToInteger, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.ToTimestamp))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.ToTimestamp, stringResults));
            }
            else if (isFunction && callNode.Method.Name == nameof(SqlFunction.Abs))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Abs, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(Math) && callNode.Method.Name == nameof(Math.Abs))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Abs, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(string) && callNode.Method.Name == nameof(string.StartsWith))
            {
                stringResults[1] = mSpecifics.GetSqlFunction(SqlFunctionId.Concat, new string[] { stringResults[1], "'%'" });
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Like, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(string) && callNode.Method.Name == nameof(string.ToUpper))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Lower, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(string) && callNode.Method.Name == nameof(string.ToUpper))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Lower, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(string) && callNode.Method.Name == nameof(string.Trim))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.Trim, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(string) && callNode.Method.Name == nameof(string.TrimStart))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.TrimLeft, stringResults));
            }
            else if (callNode.Method.DeclaringType == typeof(string) && callNode.Method.Name == nameof(string.TrimEnd))
            {
                res.Expression.Append(mSpecifics.GetSqlFunction(SqlFunctionId.TrimRight, stringResults));
            }
            else
                throw new Exception("Unknown function");

            return res;
        }
    }
}
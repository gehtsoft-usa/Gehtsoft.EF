using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Gehtsoft.ExpressionToJs;

namespace Gehtsoft.Validator.JSConvertor
{
    public class ValidationExpressionCompiler : ExpressionCompiler
    {
        private readonly ParameterExpression mEntityParameter = null;
        private readonly ParameterExpression mValueParameter = null;

        public ValidationExpressionCompiler(LambdaExpression lambdaExpression, int? entityParameterIndex = null, int? valueParameterIndex = null) : base(lambdaExpression)
        {
            if ((lambdaExpression.Parameters.Count < 1 && (entityParameterIndex != null || valueParameterIndex != null)) || lambdaExpression.Parameters.Count > 2)
                throw new ArgumentException("The expression must have only one or two parameters", nameof(Expression));

            if (entityParameterIndex != null)
                mEntityParameter = lambdaExpression.Parameters[(int) entityParameterIndex];
            if (valueParameterIndex != null)
                mValueParameter = lambdaExpression.Parameters[(int) valueParameterIndex];
        }

        protected bool InLambdaParameter { get; private set; } = false;

        protected override string AddLambdaParameter(LambdaExpression expression)
        {
            bool inLambdaParameter = InLambdaParameter;
            InLambdaParameter = true;
            string s = base.AddLambdaParameter(expression);
            InLambdaParameter = inLambdaParameter;
            return s;
        }

        protected override string AddParameter(ParameterExpression parameterExpression)
        {
            if (parameterExpression == mEntityParameter)
                return "reference()";
            else if (parameterExpression == mValueParameter)
                return "value";
            else if (InLambdaParameter)
                return base.AddParameter(parameterExpression);
            else
                throw new InvalidOperationException("Only 'value' and 'entity' parameters are supported");
        }

        protected override string AddParameterAccess(Expression expression) => AddParameterAccess(expression, true);

        private static object mCustomMutex = new object();
        private static List<Func<MemberExpression, Func<Expression, string>, string>> mCustomMembers = new List<Func<MemberExpression, Func<Expression, string>, string>>();

        public static void AddCustomMemberAccess(Func<MemberExpression, Func<Expression, string>, string> handler)
        {
            lock (mCustomMutex)
            {
                mCustomMembers.Add(handler);
            }
        }

        protected override string AddMemberAccess(MemberExpression expression)
        {
            if (mCustomMembers.Count > 0)
            {
                lock (mCustomMutex)
                {
                    for (int i = 0; i < mCustomMembers.Count; i++)
                    {
                        string s = mCustomMembers[i]?.Invoke(expression, WalkExpression);
                        if (s != null)
                            return s;
                    }
                }
            }
            return base.AddMemberAccess(expression);
        }

        private static List<Func<MethodCallExpression, Func<Expression, string>, string>> mCustomCalls = new List<Func<MethodCallExpression, Func<Expression, string>, string>>();

        public static void AddCustomCall(Func<MethodCallExpression, Func<Expression, string>, string> handler)
        {
            lock (mCustomMutex)
            {
                mCustomCalls.Add(handler);
            }
        }

        protected override string AddCall(MethodCallExpression expression)
        {
            if (mCustomCalls.Count > 0)
            {
                lock (mCustomMutex)
                {
                    for (int i = 0; i < mCustomCalls.Count; i++)
                    {
                        string s = mCustomCalls[i]?.Invoke(expression, this.WalkExpression);
                        if (s != null)
                            return s;
                    }
                }
            }
            return base.AddCall(expression);
        }

        protected virtual string AddParameterAccess(Expression expression, bool initial)
        {
            string result = null;
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression memberExpression = (MemberExpression) expression;
                result = AddParameterAccess(memberExpression.Expression, false);
                if (result != "")
                    result += ".";
                result += memberExpression.Member.Name;
            }
            else if (expression.NodeType == ExpressionType.ArrayIndex)
            {
                BinaryExpression binaryExpression = (BinaryExpression) expression;
                return $"jsv_index({AddParameterAccess(binaryExpression.Left)}, {WalkExpression(binaryExpression.Right)})";
            }
            else if (expression.NodeType == ExpressionType.Parameter)
            {
                if (expression == mValueParameter)
                    return "value";
                if (expression != mEntityParameter)
                    throw new InvalidOperationException("Only 'value' and 'entity' parameters are supported");
                return "";
            }

            if (initial)
                result = $"reference('{result}')";
            return result;
        }
    }
}
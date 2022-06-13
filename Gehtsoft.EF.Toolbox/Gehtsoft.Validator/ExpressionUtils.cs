using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.Validator
{
    public static class ExpressionUtils
    {
        public static string ExpressionToName(Expression expression)
        {
            if (expression.NodeType == ExpressionType.Lambda)
                return ExpressionToName(((LambdaExpression) expression).Body);
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression memberExpression = (MemberExpression) expression;
                if (memberExpression.Expression.NodeType == ExpressionType.Parameter)
                    return memberExpression.Member.Name;
                else
                {
                    string baseName = ExpressionToName(memberExpression.Expression);
                    return $"{baseName}.{memberExpression.Member.Name}";
                }
            }
            else if (expression.NodeType == ExpressionType.ArrayIndex)
            {
                BinaryExpression arrayExpression = (BinaryExpression) expression;
                object index = Expression.Lambda(arrayExpression.Right).Compile().DynamicInvoke();
                string baseName = ExpressionToName(arrayExpression.Left);
                return $"{baseName}[{index}]";
            }
            else if (expression.NodeType == ExpressionType.Parameter)
            {
                return "";
            }
            else if (expression.NodeType == ExpressionType.Convert)
            {
                return ExpressionToName(((UnaryExpression)expression).Operand);
            }


            throw new ArgumentException("Unexpected element in the expression", nameof(expression));
        }

        public static MemberInfo ExpressionToMemberInfo(Expression expression) => ExpressionToMemberInfo(expression, false);


        public static MemberInfo ExpressionToMemberInfo(Expression expression, bool limitToParameter)
        {
            if (expression.NodeType == ExpressionType.Lambda)
                return ExpressionToMemberInfo(((LambdaExpression) expression).Body);
            
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                MemberExpression memberExpression = (MemberExpression) expression;
                if (limitToParameter)
                    if (memberExpression.Expression.NodeType != ExpressionType.Parameter)
                        throw new ArgumentException("Unexpected element in the expression", nameof(expression));

                return memberExpression.Member;
            }
            else if (expression.NodeType == ExpressionType.Convert)
            {
                return ExpressionToMemberInfo(((UnaryExpression)expression).Operand);
            }


            throw new ArgumentException("Unexpected element in the expression", nameof(expression));
        }

    }
}

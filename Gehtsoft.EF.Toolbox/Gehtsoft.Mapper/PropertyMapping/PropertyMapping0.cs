using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Gehtsoft.EF.Mapper
{
    public class PropertyMapping
    {
        protected PropertyMapping()
        {
        }

        internal static PropertyInfo PropertyOfParameterInfo(Expression expression)
        {
            try
            {
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
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

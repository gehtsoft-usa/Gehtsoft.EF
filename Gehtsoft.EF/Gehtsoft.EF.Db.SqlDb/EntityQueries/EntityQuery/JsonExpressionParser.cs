using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Parses a member/array-index expression such as <c>e =&gt; e.Profile.Age</c> or
    /// <c>e =&gt; e.Data.ChildrenAge[0]</c> into the JSON property name (the first member after the
    /// parameter), the JSON path to the value (<c>"$.a.b[0]"</c>) and the CLR type of the value.
    /// </summary>
    internal static class JsonExpressionParser
    {
        public static void Parse(LambdaExpression expression, out string propertyName, out string jsonPath, out Type valueType)
        {
            Expression body = expression.Body;
            if (body is UnaryExpression u && (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
                body = u.Operand;

            // Steps are collected leaf-first: a member step carries a name, an array-index step carries
            // an index; each step carries the CLR type of the value it produces.
            var isIndex = new List<bool>();
            var names = new List<string>();
            var indices = new List<int>();
            var types = new List<Type>();

            Expression current = body;
            while (true)
            {
                if (current is MemberExpression member)
                {
                    isIndex.Add(false);
                    names.Add(member.Member.Name);
                    indices.Add(0);
                    types.Add(MemberType(member.Member));
                    current = member.Expression;
                }
                else if (current is BinaryExpression binary && binary.NodeType == ExpressionType.ArrayIndex)
                {
                    isIndex.Add(true);
                    names.Add(null);
                    indices.Add(ConstIndex(binary.Right, expression));
                    types.Add(binary.Type);
                    current = binary.Left;
                }
                else if (current is MethodCallExpression call && call.Method.Name == "get_Item" && call.Arguments.Count == 1)
                {
                    isIndex.Add(true);
                    names.Add(null);
                    indices.Add(ConstIndex(call.Arguments[0], expression));
                    types.Add(call.Type);
                    current = call.Object;
                }
                else
                {
                    break;
                }
            }

            if (current == null || current.NodeType != ExpressionType.Parameter || names.Count < 2)
                throw new ArgumentException("The expression must be of the form e => e.JsonProperty.Field[.Field | [index] ...]", nameof(expression));

            int rootIndex = isIndex.Count - 1;
            if (isIndex[rootIndex])
                throw new ArgumentException("The expression must start with a JSON property member", nameof(expression));

            propertyName = names[rootIndex];

            Type leaf = types[0];
            valueType = Nullable.GetUnderlyingType(leaf) ?? leaf;

            var pathBuilder = new StringBuilder("$");
            for (int i = rootIndex - 1; i >= 0; i--)
            {
                if (isIndex[i])
                    pathBuilder.Append('[').Append(indices[i]).Append(']');
                else
                    pathBuilder.Append('.').Append(names[i]);
            }
            jsonPath = pathBuilder.ToString();
        }

        private static int ConstIndex(Expression indexExpression, Expression owner)
        {
            if (indexExpression is ConstantExpression constant && constant.Value is int value)
                return value;
            throw new ArgumentException("A JSON array index must be a constant integer", nameof(owner));
        }

        private static Type MemberType(MemberInfo member)
        {
            if (member is PropertyInfo property)
                return property.PropertyType;
            if (member is System.Reflection.FieldInfo field)
                return field.FieldType;
            throw new ArgumentException("Unsupported member in the JSON expression");
        }
    }
}

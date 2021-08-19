using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Hime.Redist;

namespace Gehtsoft.EF.Test.SqlParser
{
    public static class AstNodeExtensions
    {
        private static void ToAstText(IAstNode node, StringBuilder sb)
        {
            sb.Append('[');
            sb.Append(node.Symbol);
            if (!string.IsNullOrEmpty(node.Value))
            {
                sb.Append('(');
                sb.Append(node.Value);
                sb.Append(')');
            }
            foreach (var child in node.Children)
            {
                ToAstText(child, sb);
            }
            sb.Append(']');
        }

        public static string ToAstText(this IAstNode node)
        {
            var sb = new StringBuilder();
            ToAstText(node, sb);
            return sb.ToString();
        }

        private readonly static Regex mGrab = new Regex(@"([^\]\)\(\[]+)[\]\(\[\)]");

        public static IAstNode Parse(string astNode)
        {
            int position = 0;
            return Parse(astNode, ref position);
        }

        public static IAstNode Parse(string astNode, ref int position)
        {
            AstNodeImpl node = new AstNodeImpl();
            if (astNode[position] != '[')
                throw new ArgumentException($"The string should point at [ at position {position}", nameof(astNode));
            position++;
            Match m = mGrab.Match(astNode, position);
            if (!m.Success || m.Index != position)
                throw new ArgumentException($"The string should have an id at position {position}", nameof(astNode));
            node.Symbol = m.Groups[1].Value;
            position += m.Groups[1].Length;

            while (position < astNode.Length)
            {
                if (astNode[position] == '(')
                {
                    position++;
                    m = mGrab.Match(astNode, position);
                    if (!m.Success || m.Index != position)
                        throw new ArgumentException($"The string have a value at position {position}", nameof(astNode));
                    if (!m.Groups[0].Value.EndsWith(')'))
                        throw new ArgumentException($"The value at position {position} must ends with )", nameof(astNode));
                    node.Value = m.Groups[1].Value;
                    position += m.Groups[0].Length;
                    continue;
                }

                if (astNode[position] == '[')
                {
                    node.Add(Parse(astNode, ref position));
                    continue;
                }

                if (astNode[position] == ']')
                {
                    position++;
                    return node;
                }
            }
            return node;
        }

        public static IEnumerable<IAstNode> ScanChildren(this IAstNode node, bool recursive, string symbol, string value, int? index)
        {
            symbol = symbol.Replace('*', '%');
            value = value?.Replace('*', '%');
            int match = 0;

            foreach (var child in node.Children)
            {
                if (SqlFunction.Like(child.Symbol, symbol))
                {
                    if (string.IsNullOrEmpty(value) || SqlFunction.Like(child.Value, value))
                    {
                        match++;
                        if (index == null || index == match)
                            yield return child;
                    }
                }
                if (recursive)
                {
                    foreach (var child1 in ScanChildren(child, true, symbol, value, index))
                        yield return child1;
                }
            }
        }

        public static IEnumerable<IAstNode> ScanChildren(this IEnumerable<IAstNode> node, bool recursive, string symbol, string value, int? index)
        {
            foreach (var n in node)
                foreach (var c in ScanChildren(n, recursive, symbol, value, index))
                    yield return c;
        }

        public static IEnumerable<IAstNode> Self(this IAstNode node)
        {
            yield return node;
        }

        private readonly static Regex parser = new Regex(@"([^\(\/^\[]+)(\(([^)]+)\))?(\[(\d+)\])?");

        private static IEnumerable<IAstNode> ExecuteOneLevel(IEnumerable<IAstNode> target, string expression, int position)
        {
            int countSlashes = 0;
            while (position < expression.Length)
            {
                if (expression[position] == '/')
                {
                    countSlashes++;
                    position++;
                    continue;
                }
                break;
            }
            Match m = parser.Match(expression, position);
            if (!m.Success || m.Index != position)
                throw new ArgumentException($"Can't recognize predicate at {position}", nameof(expression));
            var symbol = m.Groups[1].Value;
            var value = m.Groups[3].Value;
            var _index = m.Groups[5].Value;
            int? index = null;
            if (!string.IsNullOrEmpty(_index))
                index = int.Parse(_index);
            position += m.Groups[0].Length;

            var recurive = countSlashes > 1;
            var r = ScanChildren(target, recurive, symbol, value, index);

            if (position >= expression.Length)
                return r;
            else
                return ExecuteOneLevel(r, expression, position);
        }

        public static IEnumerable<IAstNode> Select(this IAstNode node, string astPathExpression)
            => ExecuteOneLevel(Self(node), astPathExpression, 0);

        public static IAstNode SelectNode(this IAstNode node, string astPathExpression)
            => ExecuteOneLevel(Self(node), astPathExpression, 0).FirstOrDefault();

        public static AstNodeAssertions Should(this IAstNode node) => new AstNodeAssertions(node);
    }
}

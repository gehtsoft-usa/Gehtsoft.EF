using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Field or aggr func calls with possible alias
    /// </summary>
    internal class SqlExpressionAlias
    {
        internal SqlBaseExpression Expression { get; } = null;
        internal string Alias { get; private set; } = null;
        internal void SetAlias(string alias) => Alias = alias;
        internal SqlExpressionAlias(SqlStatement parentStatement, SqlParser.ExprAliasContext fieldAliasNode, string source)
        {
            parentStatement.IgnoreAlias = true;
            Expression = SqlExpressionParser.ParseExpression(parentStatement, fieldAliasNode.expr(), source);
            parentStatement.IgnoreAlias = false;
            if (fieldAliasNode.IDENTIFIER() != null)
            {
                Alias = fieldAliasNode.IDENTIFIER().GetText();
            }
            try
            {
                Alias = parentStatement.AddAliasEntry(Alias, Expression);
            }
            catch
            {
                throw new SqlParserException(new SqlError(source,
                    fieldAliasNode.Line(),
                    fieldAliasNode.Col(),
                    $"Duplicate alias name '{Alias}'"));
            }
        }
    }

    /// <summary>
    /// A collection of fields or aggr func calls with possible alias
    /// </summary>
    [Serializable]
    internal class SqlExpressionAliasCollection : IReadOnlyList<SqlExpressionAlias>
    {
        private readonly List<SqlExpressionAlias> mList = new List<SqlExpressionAlias>();

        internal SqlExpressionAliasCollection()
        {
        }

        /// <summary>
        /// Returns the field or aggr func calls by its index
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SqlExpressionAlias this[int index] => ((IReadOnlyList<SqlExpressionAlias>)mList)[index];

        /// <summary>
        /// Returns the number of fields or aggr func calls
        /// </summary>
        public int Count => ((IReadOnlyCollection<SqlExpressionAlias>)mList).Count;

        public IEnumerator<SqlExpressionAlias> GetEnumerator()
        {
            return ((IEnumerable<SqlExpressionAlias>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SqlExpressionAlias fieldAlias)
        {
            mList.Add(fieldAlias);
        }
    }
}

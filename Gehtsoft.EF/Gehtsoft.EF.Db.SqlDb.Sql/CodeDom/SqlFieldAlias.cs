using Hime.Redist;
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
        internal SqlExpressionAlias(SqlStatement parentStatement, ASTNode fieldAliasNode, string source)
        {
            parentStatement.IgnoreAlias = true;
            Expression = SqlExpressionParser.ParseExpression(parentStatement, fieldAliasNode.Children[0], source);
            parentStatement.IgnoreAlias = false;
            if (fieldAliasNode.Children.Count > 1)
            {
                Alias = fieldAliasNode.Children[1].Value;
            }
            try
            {
                Alias = parentStatement.AddAliasEntry(Alias, Expression);
            }
            catch
            {
                throw new SqlParserException(new SqlError(source,
                    fieldAliasNode.Position.Line,
                    fieldAliasNode.Position.Column,
                    $"Duplicate alias name '{Alias}'"));
            }
        }
        internal SqlExpressionAlias(SqlStatement parentStatement, SqlBaseExpression expression, string alias)
        {
            Expression = expression;
            Alias = alias;
            try
            {
                Alias = parentStatement.AddAliasEntry(Alias, Expression);
            }
            catch
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Duplicate alias name '{Alias}'"));
            }
        }
        internal SqlExpressionAlias(SqlStatement parentStatement, SqlBaseExpression expression)
        {
            Expression = expression;
            Alias = parentStatement.AddAliasEntry(Alias, Expression);
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

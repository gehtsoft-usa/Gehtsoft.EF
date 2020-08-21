using Hime.Redist;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Field or aggr func calls with possible alias
    /// </summary>
    public class SqlExpressionAlias : IEquatable<SqlExpressionAlias>
    {
        public SqlBaseExpression Expression { get; } = null;
        public string Alias { get; } = null;
        internal SqlExpressionAlias(SqlStatement parentStatement, ASTNode fieldAliasNode, string source)
        {
            parentStatement.IgnoreAlias = true;
            Expression = SqlExpressionParser.ParseExpression(parentStatement, fieldAliasNode.Children[0], source);
            parentStatement.IgnoreAlias = false;
            if (fieldAliasNode.Children.Count > 1)
            {
                Alias = fieldAliasNode.Children[1].Value;
                try
                {
                    parentStatement.AddAliasEntry(Alias, Expression);
                }
                catch
                {
                    throw new SqlParserException(new SqlError(source,
                        fieldAliasNode.Position.Line,
                        fieldAliasNode.Position.Column,
                        $"Duplicate alias name '{Alias}'"));
                }
            }
        }
        internal SqlExpressionAlias(SqlStatement parentStatement, SqlBaseExpression expression, string alias)
        {
            Expression = expression;
            Alias = alias;
            try
            {
                parentStatement.AddAliasEntry(Alias, Expression);
            }
            catch
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Duplicate alias name '{Alias}'"));
            }
        }
        internal SqlExpressionAlias(SqlBaseExpression expression)
        {
            Expression = expression;
            Alias = null;
        }

        public virtual bool Equals(SqlExpressionAlias other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (!(this.Expression == null ? (other.Expression == null) : Expression.Equals(other.Expression)))
                return false;
            return this.Alias == null?(other.Alias == null) : this.Alias == other.Alias;
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlExpressionAlias item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// A collection of fields or aggr func calls with possible alias
    /// </summary>
    [Serializable]
    public class SqlExpressionAliasCollection : IReadOnlyList<SqlExpressionAlias>
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

        internal void Add(SqlExpressionAlias fieldAlias) => mList.Add(fieldAlias);
    }

}

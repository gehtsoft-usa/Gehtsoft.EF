using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SqlWhereClause : IEquatable<SqlWhereClause>
    {

        public SqlBaseExpression RootExpression { get; internal set; }

        internal SqlWhereClause(SqlStatement parentStatement, ASTNode statementNode, string source)
        {
            RootExpression = SqlExpressionParser.ParseExpression(parentStatement, statementNode.Children[0], source);
            if (RootExpression == null)
            {
                throw new SqlParserException(new SqlError(source,
                    statementNode.Position.Line,
                    statementNode.Position.Column,
                    $"Unexpected or incorrect expression node {statementNode.Symbol.Name}({statementNode.Value ?? "null"})"));
            }
        }

        internal SqlWhereClause(SqlBaseExpression rootExpression)
        {
            RootExpression = rootExpression;
            if (RootExpression == null)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Incorrect expression node"));
            }
        }

        internal SqlWhereClause()
        {
        }

        public virtual bool Equals(SqlWhereClause other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return this.RootExpression.Equals(other.RootExpression);
        }

        public override bool Equals(object obj)
        {
            if (obj is SqlWhereClause item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}

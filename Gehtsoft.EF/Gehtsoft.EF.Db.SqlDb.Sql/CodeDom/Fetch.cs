using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class Fetch : SqlBaseExpression
    {
        public SqlBaseExpression Parameter { get; }

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Fetch;
            }
        }
        public override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Row;
            }
        }

        internal Fetch(Statement parentStatement, ASTNode fieldNode, string source)
        {
            ASTNode expressionNode = fieldNode.Children[0];
            Parameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (Parameter.ResultType != ResultTypes.Cursor)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"No cursor parameter in FETCH function call ({expressionNode.Value ?? "null"})"));
            }
            if (!Statement.IsCalculable(Parameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Not calculable parameter in FETCH function call ({expressionNode.Value ?? "null"})"));
            }
        }

        internal Fetch(SqlBaseExpression parameter)
        {
            Parameter = parameter;
            if (Parameter.ResultType != ResultTypes.Cursor)
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"No cursor parameter in FETCH function call"));
            }
            if (!Statement.IsCalculable(Parameter))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"Not calculable parameter in FETCH function call"));
            }
        }

        public virtual bool Equals(Fetch other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return this.Parameter.Equals(other.Parameter);
        }

        public override bool Equals(SqlBaseExpression obj)
        {
            if (obj is Fetch item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}

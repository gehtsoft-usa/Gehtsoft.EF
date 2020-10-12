using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class GetRowsCount : SqlBaseExpression
    {
        internal  SqlBaseExpression Parameter { get; }

        internal  override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetRowsCount;
            }
        }
        internal  override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Integer;
            }
        }

        internal GetRowsCount(Statement parentStatement, ASTNode fieldNode, string source)
        {
            ASTNode expressionNode = fieldNode.Children[0];
            Parameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (Parameter.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"No ROWSET parameter in GET_ROWS function call ({expressionNode.Value ?? "null"})"));
            }
            if (!Statement.IsCalculable(Parameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Not calculable parameter in GET_ROWS function call ({expressionNode.Value ?? "null"})"));
            }
        }

        internal GetRowsCount(SqlBaseExpression parameter)
        {
            Parameter = parameter;
            if (Parameter.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(null,0,0,
                    $"No ROWSET parameter in GET_ROWS function call"));
            }
            if (!Statement.IsCalculable(Parameter))
            {
                throw new SqlParserException(new SqlError(null, 0, 0,
                    $"Not calculable parameter in GET_ROWS function call"));
            }
        }

        internal  virtual bool Equals(GetRowsCount other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            return this.Parameter.Equals(other.Parameter);
        }

        internal override bool Equals(SqlBaseExpression obj)
        {
            if (obj is GetRowsCount item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}

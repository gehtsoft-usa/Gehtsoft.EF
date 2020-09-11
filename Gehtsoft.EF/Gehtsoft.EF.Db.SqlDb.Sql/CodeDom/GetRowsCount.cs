using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class GetRowsCount : SqlBaseExpression
    {
        public SqlBaseExpression Parameter { get; }

        public override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetRowsCount;
            }
        }
        public override ResultTypes ResultType
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
    }
}

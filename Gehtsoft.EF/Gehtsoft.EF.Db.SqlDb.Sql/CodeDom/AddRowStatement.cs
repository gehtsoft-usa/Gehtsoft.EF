using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class AddRowStatement : Statement
    {
        public SqlBaseExpression ValueExpression { get; }
        public GlobalParameter RowParameter { get; }
        internal AddRowStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.AddRow)
        {
            ASTNode expressionNode;
            expressionNode = statementNode.Children[0];
            ValueExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (ValueExpression.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Row expression in ADD ROW statement should have ROW type"));
            }
            if (!Statement.IsCalculable(ValueExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"Not calculable value expression in ADD ROW statement"));
            }

            expressionNode = statementNode.Children[1];
            SqlBaseExpression rowExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (rowExpression.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"RowSet expression in ADD ROW statement should have ROWSET type"));
            }
            if (rowExpression.ExpressionType != ExpressionTypes.GlobalParameter)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    $"RowSet should be set by global variable in ADD ROW statement"));
            }
            RowParameter = (GlobalParameter)rowExpression;
        }

        internal void Run(SqlDbConnection connection)
        {
            object value = StatementRunner.CalculateExpression(ValueExpression, CodeDomBuilder, connection).Value;
            string globalVariableName = RowParameter.Name;
            List<object> list = (List<object>)CodeDomBuilder.FindGlobalParameter(globalVariableName).Value;
            list.Add(value);
       }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null, Expression.Constant(CodeDomBuilder.Connection));
        }
    }
}

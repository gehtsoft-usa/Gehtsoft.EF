using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class AddRowStatement : Statement
    {
        internal SqlBaseExpression ValueExpression { get; }
        internal GlobalParameter RowParameter { get; }
        internal AddRowStatement(SqlCodeDomBuilder builder, SqlParser.AddRowStatementContext statementNode, string currentSource)
            : base(builder, StatementType.AddRow)
        {
            SqlParser.ExprContext expressionNode = statementNode.expr();
            ValueExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (ValueExpression.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Line(),
                    expressionNode.Col(),
                    "Row expression in ADD ROW statement should have ROW type"));
            }
            if (!Statement.IsCalculable(ValueExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Line(),
                    expressionNode.Col(),
                    "Not calculable value expression in ADD ROW statement"));
            }

            SqlParser.GlobalParameterSimpleContext rowNode = statementNode.globalParameterSimple();
            GlobalParameter rowExpression = new GlobalParameter(this, rowNode);
            if (rowExpression.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    rowNode.Line(),
                    rowNode.Col(),
                    "RowSet expression in ADD ROW statement should have ROWSET type"));
            }
            RowParameter = rowExpression;
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

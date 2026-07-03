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
    internal class AddFieldStatement : Statement
    {
        internal SqlBaseExpression FieldNameExpression { get; }
        internal SqlBaseExpression ValueExpression { get; }
        internal GlobalParameter RowParameter { get; }
        internal AddFieldStatement(SqlCodeDomBuilder builder, SqlParser.AddFieldStatementContext statementNode, string currentSource)
            : base(builder, StatementType.AddField)
        {
            SqlParser.ExprContext expressionNode = statementNode.expr(0);
            FieldNameExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (FieldNameExpression.ResultType != ResultTypes.String)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Line(),
                    expressionNode.Col(),
                    "Field name expression in ADD FIELD statement should have STRING type"));
            }
            if (!Statement.IsCalculable(FieldNameExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Line(),
                    expressionNode.Col(),
                    "Not calculable field name expression in ADD FIELD statement"));
            }

            expressionNode = statementNode.expr(1);
            ValueExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (!Statement.IsCalculable(ValueExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Line(),
                    expressionNode.Col(),
                    "Not calculable value expression in ADD FIELD statement"));
            }

            SqlParser.GlobalParameterSimpleContext rowNode = statementNode.globalParameterSimple();
            GlobalParameter rowExpression = new GlobalParameter(this, rowNode);
            if (rowExpression.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    rowNode.Line(),
                    rowNode.Col(),
                    "Row expression in ADD FIELD statement should have ROW type"));
            }
            RowParameter = rowExpression;
        }

        internal void Run(SqlDbConnection connection)
        {
            string fieldName = (string)StatementRunner.CalculateExpression(FieldNameExpression, CodeDomBuilder, connection).Value;
            object value = StatementRunner.CalculateExpression(ValueExpression, CodeDomBuilder, connection).Value;
            string globalVariableName = RowParameter.Name;
            IDictionary<string, object> record = (IDictionary<string, object>)CodeDomBuilder.FindGlobalParameter(globalVariableName).Value;
            if (record.ContainsKey(fieldName))
                record[fieldName] = value;
            else
                record.Add(fieldName, value);
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null, Expression.Constant(CodeDomBuilder.Connection));
        }
    }
}

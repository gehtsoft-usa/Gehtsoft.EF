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
    internal class AddFieldStatement : Statement
    {
        internal SqlBaseExpression FieldNameExpression { get; }
        internal SqlBaseExpression ValueExpression { get; }
        internal GlobalParameter RowParameter { get; }
        internal AddFieldStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.AddField)
        {
            ASTNode expressionNode = statementNode.Children[0];
            FieldNameExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (FieldNameExpression.ResultType != ResultTypes.String)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Field name expression in ADD FIELD statement should have STRING type"));
            }
            if (!Statement.IsCalculable(FieldNameExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Not calculable field name expression in ADD FIELD statement"));
            }

            expressionNode = statementNode.Children[1];
            ValueExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (!Statement.IsCalculable(ValueExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Not calculable value expression in ADD FIELD statement"));
            }

            expressionNode = statementNode.Children[2];
            SqlBaseExpression rowExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
            if (rowExpression.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Row expression in ADD FIELD statement should have ROW type"));
            }
            if (rowExpression.ExpressionType != ExpressionTypes.GlobalParameter)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    expressionNode.Position.Line,
                    expressionNode.Position.Column,
                    "Row should be set by global variable in ADD FIELD statement"));
            }
            RowParameter = (GlobalParameter)rowExpression;
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

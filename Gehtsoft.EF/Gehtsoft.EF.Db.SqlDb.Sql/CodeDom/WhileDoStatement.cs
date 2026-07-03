using System.Collections.Generic;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class WhileDoStatement : BlockStatement
    {
        internal WhileDoStatement(SqlCodeDomBuilder builder, SqlParser.WhileStatementContext statementNode, string currentSource)
            : base(builder, StatementType.Loop)
        {
            SqlParser.ExprContext conditionNode = statementNode.expr();
            SqlBaseExpression whileExpression = SqlExpressionParser.ParseExpression(this, conditionNode, currentSource);
            if (!Statement.IsCalculable(whileExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    conditionNode.Line(),
                    conditionNode.Col(),
                    "Not calculable expression in WHILE statement"));
            }
            if (whileExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    conditionNode.Line(),
                    conditionNode.Col(),
                    $"While expression of LOOP should be boolean ({conditionNode.GetText()})"));
            }

            SqlParser.StatementListContext node = statementNode.statementList();
            ConditionalStatementsRun condition = new ConditionalStatementsRun(new SqlUnaryExpression(whileExpression, SqlUnaryExpression.OperationType.Not));
            IfStatement ifStatement = new IfStatement(builder, new ConditionalStatementsRunCollection() { condition });

            BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("WHILE-LOOP Body", node, this);
            List<Expression> expressionList = new List<Expression>();
            int cnt = linqExpression.Expressions.Count;

            LabelTarget startLabel = ((LabelExpression)linqExpression.Expressions[1]).Target;
            LabelTarget endLabel = ((LabelExpression)linqExpression.Expressions[cnt - 2]).Target;

            SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, this.Type);

            BreakStatement breakStatement = new BreakStatement(builder);
            condition.LinqExpression = Expression.Block(breakStatement.ToLinqWxpression(), Expression.Constant(null));

            expressionList.Add(linqExpression.Expressions[0]);
            expressionList.Add(linqExpression.Expressions[1]);
            expressionList.Add(ifStatement.ToLinqWxpression());
            for (int i = 2; i < cnt - 2; i++)
            {
                Expression expr = linqExpression.Expressions[i];
                expressionList.Add(expr);
            }
            expressionList.Add(new ContinueStatement(builder).ToLinqWxpression());
            expressionList.Add(linqExpression.Expressions[cnt - 2]);
            expressionList.Add(linqExpression.Expressions[cnt - 1]);

            LinqExpression = Expression.Block(expressionList);

            SqlCodeDomBuilder.PopDescriptor(builder);
        }
    }
}

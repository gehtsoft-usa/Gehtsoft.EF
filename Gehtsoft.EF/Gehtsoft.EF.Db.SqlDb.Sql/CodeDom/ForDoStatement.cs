using System.Collections.Generic;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class ForDoStatement : BlockStatement
    {
        internal ForDoStatement(SqlCodeDomBuilder builder, SqlParser.ForStatementContext statementNode, string currentSource)
            : base(builder, StatementType.Loop)
        {
            LabelTarget startLabel = Expression.Label();
            LabelTarget endLabel = Expression.Label();
            List<Expression> blockSet = new List<Expression>
            {
                builder.StartBlock(startLabel, endLabel, Statement.StatementType.Block),
                Expression.Label(startLabel)
            };
            SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, Statement.StatementType.Block);

            BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR Body", statementNode.statementList(0), new DummyPersistBlock(builder));
            int cnt = linqExpression.Expressions.Count;
            for (int i = 2; i < cnt - 2; i++)
            {
                blockSet.Add(linqExpression.Expressions[i]);
            }

            SqlParser.ExprContext whileNode = statementNode.expr();
            SqlBaseExpression whileExpression = SqlExpressionParser.ParseExpression(this, whileNode, currentSource);
            if (!Statement.IsCalculable(whileExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    whileNode.Line(),
                    whileNode.Col(),
                    "Not calculable expression in WHILE statement"));
            }
            if (whileExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    whileNode.Line(),
                    whileNode.Col(),
                    $"WHILE expression of LOOP should be boolean ({whileNode.GetText()})"));
            }

            List<Expression> nextSet = new List<Expression>();
            linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR-NEXT Body", statementNode.statementList(1), null);
            cnt = linqExpression.Expressions.Count;
            for (int i = 2; i < cnt - 2; i++)
            {
                nextSet.Add(linqExpression.Expressions[i]);
            }

            ConditionalStatementsRun condition = new ConditionalStatementsRun(new SqlUnaryExpression(whileExpression, SqlUnaryExpression.OperationType.Not));
            IfStatement ifStatement = new IfStatement(builder, new ConditionalStatementsRunCollection() { condition });

            this.OnContinue = Expression.Block(nextSet);
            linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR-LOOP Body", statementNode.statementList(2), this);
            List<Expression> expressionList = new List<Expression>();
            cnt = linqExpression.Expressions.Count;

            LabelTarget startLabelInner = ((LabelExpression)linqExpression.Expressions[1]).Target;
            LabelTarget endLabelInner = ((LabelExpression)linqExpression.Expressions[cnt - 2]).Target;

            SqlCodeDomBuilder.PushDescriptor(builder, startLabelInner, endLabelInner, this.Type);
            builder.BlockDescriptors.Peek().OnContinue = this.OnContinue;

            BreakStatement breakStatement = new BreakStatement(builder);
            condition.LinqExpression = Expression.Block(breakStatement.ToLinqWxpression(), Expression.Constant(null));

            expressionList.Add(linqExpression.Expressions[0]);
            expressionList.Add(linqExpression.Expressions[1]);
            expressionList.Add(ifStatement.ToLinqWxpression());
            for (int i = 2; i < cnt - 2; i++)
            {
                expressionList.Add(linqExpression.Expressions[i]);
            }
            ContinueStatement cntn = new ContinueStatement(builder);
            expressionList.Add(cntn.ToLinqWxpression());
            expressionList.Add(linqExpression.Expressions[cnt - 2]);
            expressionList.Add(linqExpression.Expressions[cnt - 1]);

            blockSet.Add(Expression.Block(expressionList));

            SqlCodeDomBuilder.PopDescriptor(builder);

            SqlCodeDomBuilder.PopDescriptor(builder);
            blockSet.Add(Expression.Label(endLabel));
            blockSet.Add(builder.EndBlock());
            LinqExpression = Expression.Block(blockSet);
        }
    }
}

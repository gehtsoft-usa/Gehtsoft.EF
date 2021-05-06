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
    internal class ForDoStatement : BlockStatement
    {
        internal ForDoStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Loop)
        {
            ASTNode node;

            LabelTarget startLabel;
            LabelTarget endLabel;
            List<Expression> blockSet;
            List<Expression> nextSet;

            startLabel = Expression.Label();
            endLabel = Expression.Label();
            blockSet = new List<Expression>
            {
                builder.StartBlock(startLabel, endLabel, Statement.StatementType.Block),
                Expression.Label(startLabel)
            };
            SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, Statement.StatementType.Block);
            node = statementNode.Children[0];
            BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR Body", node, new DummyPersistBlock(builder));
            int cnt = linqExpression.Expressions.Count;
            for (int i = 2; i < cnt - 2; i++)
            {
                Expression expr = linqExpression.Expressions[i];
                blockSet.Add(expr);
            }
            node = statementNode.Children[1];
            SqlBaseExpression whileExpression = SqlExpressionParser.ParseExpression(this, node, currentSource);
            if (!Statement.IsCalculable(whileExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    node.Position.Line,
                    node.Position.Column,
                    "Not calculable expression in WHILE statement"));
            }
            if (whileExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    node.Position.Line,
                    node.Position.Column,
                    $"WHILE expression of LOOP should be boolean {node.Symbol.Name} ({node.Value ?? "null"})"));
            }

            node = statementNode.Children[2];
            nextSet = new List<Expression>();
            linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR-NEXT Body", node, null);
            cnt = linqExpression.Expressions.Count;
            for (int i = 2; i < cnt - 2; i++)
            {
                Expression expr = linqExpression.Expressions[i];
                nextSet.Add(expr);
            }

            node = statementNode.Children[3];

            ConditionalStatementsRun condition = new ConditionalStatementsRun(new SqlUnaryExpression(whileExpression, SqlUnaryExpression.OperationType.Not));
            IfStatement ifStatement = new IfStatement(builder, new ConditionalStatementsRunCollection() { condition });

            this.OnContinue = Expression.Block(nextSet);
            linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR-LOOP Body", node, this);
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
                Expression expr = linqExpression.Expressions[i];
                expressionList.Add(expr);
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

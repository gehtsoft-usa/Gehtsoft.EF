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
    internal  class ForDoStatement : BlockStatement
    {
        internal ForDoStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Loop)
        {
            ASTNode node;

            Statements = new StatementSetEnvironment();
            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;

            LabelTarget startLabel = null;
            LabelTarget endLabel = null;
            List<Expression> blockSet = null;
            List<Expression> nextSet = null;

            if (builder.WhetherParseToLinq)
            {
                startLabel = Expression.Label();
                endLabel = Expression.Label();
                blockSet = new List<Expression>();
                blockSet.Add(builder.StartBlock(startLabel, endLabel, Statement.StatementType.Block));
                blockSet.Add(Expression.Label(startLabel));
                SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, Statement.StatementType.Block);
            }
            node = statementNode.Children[0];
            Statements.Add(builder.ParseNode("FOR Body", node));
            if (builder.WhetherParseToLinq)
            {
                BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR Body", node, null);
                int cnt = linqExpression.Expressions.Count;
                for (int i = 2; i < cnt - 2; i++)
                {
                    Expression expr = linqExpression.Expressions[i];
                    blockSet.Add(expr);
                }
            }
            node = statementNode.Children[1];
            SqlBaseExpression whileExpression = SqlExpressionParser.ParseExpression(this, node, currentSource);
            if (!Statement.IsCalculable(whileExpression))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    node.Position.Line,
                    node.Position.Column,
                    $"Not calculable expression in WHILE statement"));
            }
            if (whileExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
            {
                throw new SqlParserException(new SqlError(currentSource,
                    node.Position.Line,
                    node.Position.Column,
                    $"WHILE expression of LOOP should be boolean {node.Symbol.Name} ({node.Value ?? "null"})"));
            }

            node = statementNode.Children[2];
            StatementSetEnvironment nextStatements = builder.ParseNode("FOR-NEXT Body", node);
            if (builder.WhetherParseToLinq)
            {
                nextSet = new List<Expression>();
                BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR-NEXT Body", node, null);
                int cnt = linqExpression.Expressions.Count;
                for (int i = 2; i < cnt - 2; i++)
                {
                    Expression expr = linqExpression.Expressions[i];
                    nextSet.Add(expr);
                }
            }

            node = statementNode.Children[3];

            StatementSetEnvironment loopStatements = builder.ParseNode("FOR-LOOP Body", node, this);
            Statements.Add(new BlockStatement(builder, loopStatements));

            loopStatements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = loopStatements;
            builder.TopEnvironment.ParentStatement = this;

            BreakStatement breakStatement = new BreakStatement(builder);
            ConditionalStatementsRun condition = new ConditionalStatementsRun(new SqlUnarExpression(whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { breakStatement });
            IfStatement ifStatement = new IfStatement(builder, new ConditionalStatementsRunCollection() { condition });

            loopStatements.InsertFirst(ifStatement);
            loopStatements.BeforeContinue = nextStatements;
            loopStatements.Add(new ContinueStatement(builder));

            if (builder.WhetherParseToLinq)
            {
                this.OnContinue = Expression.Block(nextSet);
                BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("FOR-LOOP Body", node, this);
                List<Expression> expressionList = new List<Expression>();
                int cnt = linqExpression.Expressions.Count;

                LabelTarget startLabelInner = ((LabelExpression)linqExpression.Expressions[1]).Target;
                LabelTarget endLabelInner = ((LabelExpression)linqExpression.Expressions[cnt - 2]).Target;

                SqlCodeDomBuilder.PushDescriptor(builder, startLabelInner, endLabelInner, this.Type);
                builder.BlockDescriptors.Peek().OnContinue = this.OnContinue;

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

            builder.TopEnvironment = Statements.ParentEnvironment;
        }

        internal ForDoStatement(SqlCodeDomBuilder builder, StatementSetEnvironment forStatements, SqlBaseExpression whileExpression,
            StatementSetEnvironment nextStatements, StatementSetEnvironment loopStatements)
            : base(builder, StatementType.Loop)
        {
            if (!Statement.IsCalculable(whileExpression))
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not calculable expression in WHILE statement"));
            }
            if (whileExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"WHILE expression of LOOP should be boolean"));
            }
            Statements = new StatementSetEnvironment();
            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;
            Statements.Add(forStatements);
            Statements.Add(new BlockStatement(builder, loopStatements));

            loopStatements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = loopStatements;
            builder.TopEnvironment.ParentStatement = this;

            loopStatements.InsertFirst(new IfStatement(builder, new ConditionalStatementsRunCollection() {
                new ConditionalStatementsRun( new SqlUnarExpression( whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { new BreakStatement(builder) })
            }));
            loopStatements.BeforeContinue = nextStatements;
            loopStatements.Add(new ContinueStatement(builder));
            builder.TopEnvironment = Statements.ParentEnvironment;
        }
    }
}

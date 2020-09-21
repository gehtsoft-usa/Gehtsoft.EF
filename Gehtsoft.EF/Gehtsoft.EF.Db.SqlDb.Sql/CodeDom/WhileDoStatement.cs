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
    public class WhileDoStatement : BlockStatement
    {
        internal WhileDoStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Loop)
        {
            ASTNode node = statementNode.Children[0];
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
                    $"While expression of LOOP should be boolean {node.Symbol.Name} ({node.Value ?? "null"})"));
            }

            node = statementNode.Children[1];
            Statements = builder.ParseNode("WHILE-LOOP Body", node, this);

            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;
            builder.TopEnvironment.ParentStatement = this;
            BreakStatement breakStatement = new BreakStatement(builder);
            ConditionalStatementsRun condition = new ConditionalStatementsRun(new SqlUnarExpression(whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { breakStatement });
            IfStatement ifStatement = new IfStatement(builder, new ConditionalStatementsRunCollection() { condition });

            Statements.InsertFirst(ifStatement);
            Statements.Add(new ContinueStatement(builder));

            if (builder.WhetherParseToLinq)
            {
                BlockExpression linqExpression = (BlockExpression)builder.ParseNodeToLinq("WHILE-LOOP Body", node, this);
                List<Expression> expressionList = new List<Expression>();
                int cnt = linqExpression.Expressions.Count;

                LabelTarget startLabel = ((LabelExpression)linqExpression.Expressions[1]).Target;
                LabelTarget endLabel = ((LabelExpression)linqExpression.Expressions[cnt - 2]).Target;

                SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, this.Type);

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
            builder.TopEnvironment = Statements.ParentEnvironment;
        }

        internal WhileDoStatement(SqlCodeDomBuilder builder, SqlBaseExpression whileExpression, StatementSetEnvironment statements)
            : base(builder, StatementType.Loop)
        {
            if (!Statement.IsCalculable(whileExpression))
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"Not calculable expression in WHILE statement"));
            }
            if (whileExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
            {
                throw new SqlParserException(new SqlError(null, 0, 0, $"While expression of LOOP should be boolean"));
            }
            Statements = statements;
            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;
            builder.TopEnvironment.ParentStatement = this;

            Statements.InsertFirst(new IfStatement(builder, new ConditionalStatementsRunCollection() {
                new ConditionalStatementsRun( new SqlUnarExpression( whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { new BreakStatement(builder) })
            }));
            Statements.Add(new ContinueStatement(builder));
            builder.TopEnvironment = Statements.ParentEnvironment;
        }
    }
}

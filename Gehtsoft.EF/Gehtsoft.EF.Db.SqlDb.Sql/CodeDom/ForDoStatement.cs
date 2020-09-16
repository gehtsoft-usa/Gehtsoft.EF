using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class ForDoStatement : BlockStatement
    {
        internal ForDoStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Loop)
        {
            ASTNode node;

            Statements = new StatementSetEnvironment();
            Statements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = Statements;

            node = statementNode.Children[0];
            Statements.Add(builder.ParseNode("FOR Body", node));

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

            node = statementNode.Children[3];

            StatementSetEnvironment loopStatements = builder.ParseNode("FOR-LOOP Body", node, this);
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

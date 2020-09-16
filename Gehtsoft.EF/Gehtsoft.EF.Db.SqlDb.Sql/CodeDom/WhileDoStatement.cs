﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

            Statements.InsertFirst(new IfStatement(builder, new ConditionalStatementsRunCollection() {
                new ConditionalStatementsRun( new SqlUnarExpression( whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { new BreakStatement(builder) })
            }));
            Statements.Add(new ContinueStatement(builder));
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

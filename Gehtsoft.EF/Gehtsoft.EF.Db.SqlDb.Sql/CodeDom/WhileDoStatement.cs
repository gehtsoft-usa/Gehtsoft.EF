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
    public class WhileDoStatement : Statement
    {
        public StatementSetEnvironment RepeatStatements { get; }

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
            RepeatStatements = builder.ParseNode("WHILE-LOOP Body", node, this);
            RepeatStatements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = RepeatStatements;
            builder.TopEnvironment.ParentStatement = this;

            RepeatStatements.InsertFirst(new IfStatement(builder, new IfItemCollection() {
                new IfItem( new SqlUnarExpression( whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { new BreakStatement(builder) })
            }));
            RepeatStatements.Add(new ContinueStatement(builder));
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
            RepeatStatements = statements;
            RepeatStatements.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = RepeatStatements;
            builder.TopEnvironment.ParentStatement = this;

            RepeatStatements.InsertFirst(new IfStatement(builder, new IfItemCollection() {
                new IfItem( new SqlUnarExpression( whileExpression, SqlUnarExpression.OperationType.Not),
                new StatementSetEnvironment() { new BreakStatement(builder) })
            }));
            RepeatStatements.Add(new ContinueStatement(builder));
        }

        public virtual bool Equals(WhileDoStatement other)
        {
            if (other is WhileDoStatement stmt)
            {
                return RepeatStatements.Equals(stmt.RepeatStatements);
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is WhileDoStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}

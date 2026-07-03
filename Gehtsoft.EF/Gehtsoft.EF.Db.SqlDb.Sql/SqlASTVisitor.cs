using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class SqlAstVisitor
    {
        /// <summary>
        /// Visit and process a single statement node (dispatches on the concrete
        /// statement context produced by the ANTLR parser).
        /// </summary>
        private Statement VisitStatement(SqlCodeDomBuilder builder, string source, SqlParser.StatementContext statementNode)
        {
            switch (statementNode.GetChild(0))
            {
                case SqlParser.SelectStatementContext c: return new SqlSelectStatement(builder, c, source);
                case SqlParser.InsertStatementContext c: return new SqlInsertStatement(builder, c, source);
                case SqlParser.UpdateStatementContext c: return new SqlUpdateStatement(builder, c, source);
                case SqlParser.DeleteStatementContext c: return new SqlDeleteStatement(builder, c, source);
                case SqlParser.SetStatementContext c: return new SetStatement(builder, c, source);
                case SqlParser.DeclareStatementContext c: return new DeclareStatement(builder, c, source);
                case SqlParser.ImportStatementContext c: return new ImportStatement(builder, c, source);
                case SqlParser.ExitStatementContext c: return new ExitStatement(builder, c, source);
                case SqlParser.IfStatementContext c: return new IfStatement(builder, c, source);
                case SqlParser.ContinueStatementContext c: return new ContinueStatement(builder, c, source);
                case SqlParser.BreakStatementContext c: return new BreakStatement(builder, c, source);
                case SqlParser.WhileStatementContext c: return new WhileDoStatement(builder, c, source);
                case SqlParser.ForStatementContext c: return new ForDoStatement(builder, c, source);
                case SqlParser.SwitchStatementContext c: return new SwitchStatement(builder, c, source);
                case SqlParser.AddFieldStatementContext c: return new AddFieldStatement(builder, c, source);
                case SqlParser.AddRowStatementContext c: return new AddRowStatement(builder, c, source);
                case SqlParser.DeclareCursorStatementContext c: return new DeclareCursorStatement(builder, c, source);
                case SqlParser.OpenCursorStatementContext c: return new OpenCursorStatement(builder, c, source);
                case SqlParser.CloseCursorStatementContext c: return new CloseCursorStatement(builder, c, source);
                case SqlParser.AssignStatementContext c: return new AssignStatement(builder, c, source);
                case SqlParser.NopContext _: return null;
            }
            throw new SqlParserException(new SqlError(source,
                statementNode.Line(),
                statementNode.Col(),
                $"Unexpected or incorrect statement ({statementNode.GetText()})"));
        }

        internal Expression VisitStatementsToLinq(SqlCodeDomBuilder builder, string source, SqlParser.StatementListContext statementNode, Statement.StatementType statementType, Expression onContinue, bool clear)
        {
            LabelTarget startLabel = Expression.Label();
            LabelTarget endLabel = Expression.Label();
            List<Expression> initialSet = new List<Expression>();
            if (statementNode == null || statementNode.statement().Length == 0)
                return null;
            initialSet.Add(builder.StartBlock(startLabel, endLabel, statementType));
            initialSet.Add(Expression.Label(startLabel));
            SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, statementType);
            builder.BlockDescriptors.Peek().OnContinue = onContinue;
            foreach (SqlParser.StatementContext statementContext in statementNode.statement())
            {
                var stmt = VisitStatement(builder, source, statementContext);
                if (stmt != null)
                    initialSet.Add(stmt.ToLinqWxpression());
            }

            SqlCodeDomBuilder.PopDescriptor(builder);
            initialSet.Add(Expression.Label(endLabel));
            if (clear)
            {
                initialSet.Add(Expression.Call(Expression.Constant(builder), "ClearOpenedQueries", null));
            }
            initialSet.Add(builder.EndBlock());
            return Expression.Block(initialSet);
        }
    }
}

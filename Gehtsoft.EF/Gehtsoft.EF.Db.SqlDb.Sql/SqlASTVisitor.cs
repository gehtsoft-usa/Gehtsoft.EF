using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    internal class SqlASTVisitor
    {
        /// <summary>
        /// Visit and process a statement node
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="source"></param>
        /// <param name="statementNode"></param>
        /// <returns></returns>
        public Statement VisitStatement(SqlCodeDomBuilder builder, string source, ASTNode statementNode)
        {
            if (statementNode.Symbol.ID == SqlParser.ID.VariableStatement)
            {
                statementNode = statementNode.Children[0];
                switch (statementNode.Symbol.ID)
                {
                    case SqlParser.ID.VariableSelect:
                        {
                            if (statementNode.Children.Count < 2)
                                break;

                            return new SqlSelectStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableInsert:
                        {
                            if (statementNode.Children.Count < 3)
                                break;

                            return new SqlInsertStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableUpdate:
                        {
                            if (statementNode.Children.Count < 2)
                                break;

                            return new SqlUpdateStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableDelete:
                        {
                            return new SqlDeleteStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableSet:
                        {
                            return new SetStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableDeclare:
                        {
                            return new DeclareStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableExit:
                        {
                            return new ExitStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableIfthen:
                        {
                            return new IfStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableContinue:
                        {
                            return new ContinueStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableBreak:
                        {
                            return new BreakStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableWhiledo:
                        {
                            return new WhileDoStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableFordo:
                        {
                            return new ForDoStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableSwitch:
                        {
                            return new SwitchStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableAddFieldTo:
                        {
                            return new AddFieldStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableAddRowTo:
                        {
                            return new AddRowStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableDeclareCursor:
                        {
                            return new DeclareCursorStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableOpenCursor:
                        {
                            return new OpenCursorStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableCloseCursor:
                        {
                            return new CloseCursorStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableAssignExpr:
                        {
                            return new AssignStatement(builder, statementNode, source);
                        }
                    case SqlParser.ID.VariableNop:
                        return null;
                }
            }
            throw new SqlParserException(new SqlError(source,
                statementNode.Position.Line,
                statementNode.Position.Column,
                $"Unexpected or incorrect node {statementNode.Symbol.Name}({statementNode.Value ?? "null"})"));
        }

        /// <summary>
        /// Visit and process a sequence of the statements
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="source"></param>
        /// <param name="statementNode"></param>
        /// <returns></returns>
        public StatementSetEnvironment VisitStatements(SqlCodeDomBuilder builder, string source, ASTNode statementNode, StatementSetEnvironment initialSet)
        {
            if (statementNode.Children.Count == 0)
                return null;
            initialSet.ParentEnvironment = builder.TopEnvironment;
            builder.TopEnvironment = initialSet;
            for (int i = 0; i < statementNode.Children.Count; i++)
            {
                var stmt = VisitStatement(builder, source, statementNode.Children[i]);
                if (stmt != null && !(stmt is DeclareStatement))
                    initialSet.Add(stmt);
            }

            initialSet.FixInitialGobalParameters();
            builder.TopEnvironment = builder.TopEnvironment.ParentEnvironment;
            return initialSet;
        }
        public Expression VisitStatementsToLinq(SqlCodeDomBuilder builder, string source, ASTNode statementNode, Statement.StatementType statementType, Expression onContinue, bool clear)
        {
            LabelTarget startLabel = Expression.Label();
            LabelTarget endLabel = Expression.Label();
            List<Expression> initialSet = new List<Expression>();
            if (statementNode.Children.Count == 0)
                return null;
            initialSet.Add(builder.StartBlock(startLabel, endLabel, statementType));
            initialSet.Add(Expression.Label(startLabel));
            SqlCodeDomBuilder.PushDescriptor(builder, startLabel, endLabel, statementType);
            builder.BlockDescriptors.Peek().OnContinue = onContinue;
            for (int i = 0; i < statementNode.Children.Count; i++)
            {
                var stmt = VisitStatement(builder, source, statementNode.Children[i]);
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

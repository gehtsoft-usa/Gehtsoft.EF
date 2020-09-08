using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

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
        public SqlStatement VisitStatement(SqlCodeDomBuilder builder, string source, ASTNode statementNode)
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
        public SqlStatementCollection VisitStatements(SqlCodeDomBuilder builder, string source, ASTNode statementNode)
        {
            SqlStatementCollection r = null;

            if (statementNode.Symbol.ID == SqlParser.ID.VariableRoot)
            {
                if (statementNode.Children.Count == 0)
                    return null;
                r = new SqlStatementCollection();
                for (int i = 0; i < statementNode.Children.Count; i++)
                {
                    var stmt = VisitStatement(builder, source, statementNode.Children[i]);
                    if (stmt != null)
                        r.Add(stmt);
                }
                return r;
            }

            throw new SqlParserException(new SqlError(source,
                statementNode.Position.Line,
                statementNode.Position.Column,
                $"Unexpected parser element {statementNode.Symbol.Name}({statementNode.Value ?? "null"})"));
        }
    }
}

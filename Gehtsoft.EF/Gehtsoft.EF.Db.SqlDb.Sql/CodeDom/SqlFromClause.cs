using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlFromClause
    {
        internal SqlTableSpecificationCollection TableCollection { get; } = null;
        internal SqlFromClause(SqlStatement parentStatement, ASTNode statementNode, string source)
        {
            if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableTableReferenceList)
            {
                TableCollection = new SqlTableSpecificationCollection();
                ASTNode tableReferenceCollectionNode = statementNode.Children[0];
                foreach (ASTNode tableReferenceNode in tableReferenceCollectionNode.Children)
                {
                    if (tableReferenceNode.Symbol.ID == SqlParser.ID.VariableTablePrimary)
                    {
                        TableCollection.Add(new SqlPrimaryTable(parentStatement, tableReferenceNode, source));
                    }
                    else if (tableReferenceNode.Symbol.ID == SqlParser.ID.VariableQualifiedJoin)
                    {
                        TableCollection.Add(new SqlQualifiedJoinedTable(parentStatement, tableReferenceNode, source));
                    }
                    else if (tableReferenceNode.Symbol.ID == SqlParser.ID.VariableAutoJoin)
                    {
                        TableCollection.Add(new SqlAutoJoinedTable(parentStatement, tableReferenceNode, source));
                    }
                    else
                    {
                        throw new SqlParserException(new SqlError(source,
                            tableReferenceCollectionNode.Position.Line,
                            tableReferenceCollectionNode.Position.Column,
                            $"Unexpected table reference node {statementNode.Symbol.Name}({tableReferenceCollectionNode.Value ?? "null"})"));
                    }
                }
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    statementNode.Position.Line,
                    statementNode.Position.Column,
                    $"Unexpected or incorrect node {statementNode.Symbol.Name}({statementNode.Value ?? "null"})"));
            }
        }

        internal SqlFromClause(SqlTableSpecificationCollection tableCollection)
        {
            TableCollection = tableCollection;
        }
    }
}

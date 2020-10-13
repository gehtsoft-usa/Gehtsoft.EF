using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlSelectList
    {
        internal bool All { get; }
        internal SqlExpressionAliasCollection FieldAliasCollection { get; } = null;

        internal SqlSelectList(SqlStatement parentStatement, ASTNode statementNode, string source)
        {
            if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableAsrerisk)
                All = true;
            else if (statementNode.Children[0].Symbol.ID == SqlParser.ID.VariableSelectSublist)
            {
                All = false;
                FieldAliasCollection = new SqlExpressionAliasCollection();
                ASTNode expressionAliasCollectionNode = statementNode.Children[0];
                foreach (ASTNode expressionAliasNode in expressionAliasCollectionNode.Children)
                {
                    FieldAliasCollection.Add(new SqlExpressionAlias(parentStatement, expressionAliasNode, source));
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

        internal SqlSelectList()
        {
            All = true;
        }

        internal SqlSelectList(SqlExpressionAliasCollection fieldAliasCollection)
        {
            All = false;
            FieldAliasCollection = fieldAliasCollection;
        }
    }
}

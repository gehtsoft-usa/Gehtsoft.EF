namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlSelectList
    {
        internal bool All { get; }
        internal SqlExpressionAliasCollection FieldAliasCollection { get; } = null;

        internal SqlSelectList(SqlStatement parentStatement, SqlParser.SelectListContext statementNode, string source)
        {
            if (statementNode is SqlParser.SelectAllContext)
            {
                All = true;
            }
            else if (statementNode is SqlParser.SelectItemsContext items)
            {
                All = false;
                FieldAliasCollection = new SqlExpressionAliasCollection();
                foreach (SqlParser.ExprAliasContext expressionAliasNode in items.selectSublist().exprAlias())
                {
                    FieldAliasCollection.Add(new SqlExpressionAlias(parentStatement, expressionAliasNode, source));
                }
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    statementNode.Line(),
                    statementNode.Col(),
                    $"Unexpected or incorrect node ({statementNode.GetText()})"));
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

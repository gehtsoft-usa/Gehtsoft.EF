namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlWhereClause
    {
        internal SqlBaseExpression RootExpression { get; set; }

        internal SqlWhereClause(SqlStatement parentStatement, SqlParser.WhereClauseContext statementNode, string source)
        {
            RootExpression = SqlExpressionParser.ParseExpression(parentStatement, statementNode.expr(), source);
            if (RootExpression == null)
            {
                throw new SqlParserException(new SqlError(source,
                    statementNode.Line(),
                    statementNode.Col(),
                    $"Unexpected or incorrect expression node ({statementNode.GetText()})"));
            }
        }
    }
}

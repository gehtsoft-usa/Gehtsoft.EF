namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class GetRowsCount : SqlBaseExpression
    {
        internal SqlBaseExpression Parameter { get; }

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetRowsCount;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Integer;
            }
        }

        internal GetRowsCount(Statement parentStatement, SqlParser.RowsCountCallContext fieldNode, string source)
        {
            SqlParser.ExprContext expressionNode = fieldNode.expr();
            Parameter = SqlExpressionParser.ParseExpression(parentStatement, expressionNode, source);
            if (Parameter.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Line(), expressionNode.Col(),
                    $"No ROWSET parameter in GET_ROWS function call ({expressionNode.GetText()})"));
            }
            if (!Statement.IsCalculable(Parameter))
            {
                throw new SqlParserException(new SqlError(source,
                    expressionNode.Line(), expressionNode.Col(),
                    $"Not calculable parameter in GET_ROWS function call ({expressionNode.GetText()})"));
            }
        }
    }
}

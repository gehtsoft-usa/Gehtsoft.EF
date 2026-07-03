namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class GetRow : SqlBaseExpression
    {
        internal SqlBaseExpression RowSetParameter { get; }
        internal SqlBaseExpression IndexParameter { get; }

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetRow;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Row;
            }
        }

        internal GetRow(Statement parentStatement, SqlParser.GetRowCallContext fieldNode, string source)
        {
            SqlParser.ExprContext rowSetNode = fieldNode.expr(0);
            RowSetParameter = SqlExpressionParser.ParseExpression(parentStatement, rowSetNode, source);
            if (RowSetParameter.ResultType != ResultTypes.RowSet)
            {
                throw new SqlParserException(new SqlError(source,
                    rowSetNode.Line(), rowSetNode.Col(),
                    "No ROWSET parameter in GET_ROW function call"));
            }
            if (!Statement.IsCalculable(RowSetParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    rowSetNode.Line(), rowSetNode.Col(),
                    "Not calculable parameter in GET_ROW function call"));
            }

            SqlParser.ExprContext indexNode = fieldNode.expr(1);
            IndexParameter = SqlExpressionParser.ParseExpression(parentStatement, indexNode, source);
            if (IndexParameter.ResultType != ResultTypes.Integer)
            {
                throw new SqlParserException(new SqlError(source,
                    indexNode.Line(), indexNode.Col(),
                    "No index parameter in GET_ROW function call"));
            }
            if (!Statement.IsCalculable(IndexParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    indexNode.Line(), indexNode.Col(),
                    "Not calculable index parameter in GET_ROW function call"));
            }
        }
    }
}

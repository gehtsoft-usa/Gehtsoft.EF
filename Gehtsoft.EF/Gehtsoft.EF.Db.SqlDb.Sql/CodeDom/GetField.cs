namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class GetField : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Unknown;
        internal SqlBaseExpression RowParameter { get; }
        internal SqlBaseExpression NameParameter { get; }

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.GetField;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal GetField(Statement parentStatement, SqlParser.GetFieldCallContext fieldNode, string source)
        {
            SqlParser.ExprContext rowNode = fieldNode.expr(0);
            RowParameter = SqlExpressionParser.ParseExpression(parentStatement, rowNode, source);
            if (RowParameter.ResultType != ResultTypes.Row)
            {
                throw new SqlParserException(new SqlError(source,
                    rowNode.Line(), rowNode.Col(),
                    "No ROW parameter in GET_FIELD function call"));
            }
            if (!Statement.IsCalculable(RowParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    rowNode.Line(), rowNode.Col(),
                    "Not calculable parameter in GET_FIELD function call"));
            }

            SqlParser.ExprContext nameNode = fieldNode.expr(1);
            NameParameter = SqlExpressionParser.ParseExpression(parentStatement, nameNode, source);
            if (NameParameter.ResultType != ResultTypes.String)
            {
                throw new SqlParserException(new SqlError(source,
                    nameNode.Line(), nameNode.Col(),
                    "No valid Name parameter in GET_FIELD function call"));
            }
            if (!Statement.IsCalculable(NameParameter))
            {
                throw new SqlParserException(new SqlError(source,
                    nameNode.Line(), nameNode.Col(),
                    "Not calculable index parameter in GET_ROW function call"));
            }

            mResultType = Statement.GetResultTypeByName(fieldNode.baseType().GetText());
        }
    }
}

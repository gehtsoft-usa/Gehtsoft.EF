namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class Fetch : SqlBaseExpression
    {
        internal SqlBaseExpression Parameter { get; }

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.Fetch;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return ResultTypes.Row;
            }
        }

        internal Fetch(Statement parentStatement, SqlParser.FetchCallContext fieldNode, string source)
        {
            SqlParser.GlobalParameterSimpleContext paramNode = fieldNode.globalParameterSimple();
            Parameter = new GlobalParameter(parentStatement, paramNode);
            if (Parameter.ResultType != ResultTypes.Cursor)
            {
                throw new SqlParserException(new SqlError(source,
                    paramNode.Line(), paramNode.Col(),
                    $"No cursor parameter in FETCH function call ({paramNode.GetText()})"));
            }
            if (!Statement.IsCalculable(Parameter))
            {
                throw new SqlParserException(new SqlError(source,
                    paramNode.Line(), paramNode.Col(),
                    $"Not calculable parameter in FETCH function call ({paramNode.GetText()})"));
            }
        }
    }
}

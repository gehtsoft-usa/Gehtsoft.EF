using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlStatement;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlSelectExpression : SqlBaseExpression
    {
        private readonly ResultTypes mResultType = ResultTypes.Unknown;
        internal SqlSelectStatement SelectStatement { get; } = null;

        internal override ExpressionTypes ExpressionType
        {
            get
            {
                return ExpressionTypes.SelectExpression;
            }
        }
        internal override ResultTypes ResultType
        {
            get
            {
                return mResultType;
            }
        }

        internal SqlSelectExpression(Statement parentStatement, SqlParser.SelectExprContext exprNode, string source)
        {
            SqlParser.SelectStatementContext selectNode = exprNode.selectStatement();
            SelectStatement = new SqlSelectStatement(parentStatement.CodeDomBuilder, selectNode, source);
            if (SelectStatement.SelectList.FieldAliasCollection.Count != 1)
            {
                throw new SqlParserException(new SqlError(source,
                    selectNode.Line(), selectNode.Col(),
                    $"Expected 1 column in inner SELECT ({selectNode.GetText()})"));
            }

            mResultType = SelectStatement.SelectList.FieldAliasCollection[0].Expression.ResultType;
        }
    }
}

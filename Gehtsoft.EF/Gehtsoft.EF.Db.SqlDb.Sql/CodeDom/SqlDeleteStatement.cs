using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Delete statement
    /// </summary>
    internal class SqlDeleteStatement : SqlStatement
    {
        internal string TableName { get; } = null;
        internal SqlWhereClause WhereClause { get; } = null;

        internal SqlDeleteStatement(SqlCodeDomBuilder builder, SqlParser.DeleteStatementContext statementNode, string currentSource)
            : base(builder, StatementId.Delete, currentSource, statementNode.Line(), statementNode.Col())
        {
            TableName = statementNode.IDENTIFIER().GetText();
            try
            {
                this.AddEntityEntry(TableName, null);
            }
            catch
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Line(),
                    statementNode.Col(),
                    $"Not found entity with name '{TableName}'"));
            }

            if (statementNode.whereClause() != null)
            {
                SqlParser.WhereClauseContext whereNode = statementNode.whereClause();
                WhereClause = new SqlWhereClause(this, whereNode, currentSource);
                if (WhereClause.RootExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        whereNode.Line(),
                        whereNode.Col(),
                        $"Result of WHERE should be boolean ({whereNode.GetText()})"));
                }
                if (HasAggregateFunctions(WhereClause.RootExpression))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        whereNode.Line(),
                        whereNode.Col(),
                        $"WHERE expression should not contain calls of aggregate functions ({whereNode.GetText()})"));
                }
            }
        }

        internal override Expression ToLinqWxpression()
        {
            DeleteRunner runner = new DeleteRunner(CodeDomBuilder, CodeDomBuilder.Connection);
            return Expression.Call(Expression.Constant(runner), "RunWithResult", null, Expression.Constant(this));
        }
    }
}

using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Update statement
    /// </summary>
    internal class SqlUpdateStatement : SqlStatement
    {
        internal SqlUpdateAssignCollection UpdateAssigns { get; } = null;
        internal string TableName { get; } = null;
        internal SqlWhereClause WhereClause { get; } = null;

        internal SqlUpdateStatement(SqlCodeDomBuilder builder, SqlParser.UpdateStatementContext statementNode, string currentSource)
            : base(builder, StatementId.Update, currentSource, statementNode.Line(), statementNode.Col())
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

            UpdateAssigns = new SqlUpdateAssignCollection();
            foreach (SqlParser.UpdateAssignContext updateAssignNode in statementNode.updateList().updateAssign())
            {
                UpdateAssigns.Add(new SqlUpdateAssign(this, updateAssignNode, currentSource));
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
            UpdateRunner runner = new UpdateRunner(CodeDomBuilder, CodeDomBuilder.Connection);
            return Expression.Call(Expression.Constant(runner), "RunWithResult", null, Expression.Constant(this));
        }
    }
}

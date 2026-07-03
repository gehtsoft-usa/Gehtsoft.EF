using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    /// <summary>
    /// Select statement
    /// </summary>
    internal class SqlSelectStatement : SqlStatement
    {
        internal string SetQuantifier { get; } = string.Empty;
        internal SqlSelectList SelectList { get; } = null;
        internal SqlFromClause FromClause { get; } = null;
        internal SqlWhereClause WhereClause { get; } = null;
        internal int Offset { get; set; } = 0;
        internal int Limit { get; set; } = 0;
        internal SqlSortSpecificationCollection Sorting { get; set; } = null;
        internal SqlGroupSpecificationCollection Grouping { get; set; } = null;

        internal SqlSelectStatement(SqlCodeDomBuilder builder, SqlParser.SelectStatementContext statementNode, string currentSource)
            : base(builder, StatementId.Select, currentSource, statementNode.Line(), statementNode.Col())
        {
            if (statementNode.setQuantifier() != null)
                SetQuantifier = statementNode.setQuantifier().GetText();

            SqlParser.TableExpressionContext tableExpressionNode = statementNode.tableExpression();
            FromClause = new SqlFromClause(this, tableExpressionNode.fromClause(), currentSource);
            SelectList = new SqlSelectList(this, statementNode.selectList(), currentSource);

            if (tableExpressionNode.whereClause() != null)
            {
                SqlParser.WhereClauseContext whereNode = tableExpressionNode.whereClause();
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

            if (statementNode.limitOffset() != null)
            {
                SqlParser.LimitOffsetContext limitOffsetNode = statementNode.limitOffset();
                if (limitOffsetNode.limit() != null)
                    Limit = int.Parse(limitOffsetNode.limit().INT().GetText());
                if (limitOffsetNode.offset() != null)
                    Offset = int.Parse(limitOffsetNode.offset().INT().GetText());
            }

            if (statementNode.orderBy() != null)
            {
                Sorting = new SqlSortSpecificationCollection();
                foreach (SqlParser.SortSpecificationContext node in statementNode.orderBy().sortSpecificationList().sortSpecification())
                {
                    Sorting.Add(new SqlSortSpecification(this, node, currentSource));
                }
            }

            if (statementNode.groupBy() != null)
            {
                Grouping = new SqlGroupSpecificationCollection();
                foreach (SqlParser.GroupSpecificationContext node in statementNode.groupBy().groupSpecificationList().groupSpecification())
                {
                    Grouping.Add(new SqlGroupSpecification(this, node, currentSource));
                }
            }
        }

        internal override Expression ToLinqWxpression()
        {
            SelectRunner runner = new SelectRunner(CodeDomBuilder, CodeDomBuilder.Connection);
            return Expression.Call(Expression.Constant(runner), "RunWithResult", null, Expression.Constant(this));
        }
    }
}

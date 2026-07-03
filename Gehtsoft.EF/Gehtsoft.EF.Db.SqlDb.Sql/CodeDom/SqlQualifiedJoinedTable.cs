using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlQualifiedJoinedTable : SqlTableSpecification
    {
        private SqlBaseExpression mJoinCondition;
        private SqlParser.ExprContext mExpressionNode;
        private readonly SqlStatement mParentStatement;
        private readonly string mSource;

        internal QueryBuilderEntity BuilderEntity { get; set; }

        internal void TryExpression()
        {
            if (mExpressionNode != null)
            {
                mJoinCondition = SqlExpressionParser.ParseExpression(mParentStatement, mExpressionNode, mSource);
                if (mJoinCondition == null)
                {
                    throw new SqlParserException(new SqlError(mSource,
                        mExpressionNode.Line(),
                        mExpressionNode.Col(),
                        $"Unexpected or incorrect expression node ({mExpressionNode.GetText()})"));
                }
                if (mJoinCondition.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                {
                    throw new SqlParserException(new SqlError(mSource,
                        mExpressionNode.Line(),
                        mExpressionNode.Col(),
                        $"Result of ON should be boolean ({mExpressionNode.GetText()})"));
                }
                if (Statement.HasAggregateFunctions(mJoinCondition))
                {
                    throw new SqlParserException(new SqlError(mSource,
                        mExpressionNode.Line(),
                        mExpressionNode.Col(),
                        $"ON expression should not contain calls of aggregate functions ({mExpressionNode.GetText()})"));
                }
                mExpressionNode = null;
            }
        }

        internal SqlTableSpecification LeftTable { get; }

        internal SqlPrimaryTable RightTable { get; }

        internal string JoinType { get; }

        internal SqlBaseExpression JoinCondition
        {
            get
            {
                return mJoinCondition;
            }
        }

        internal override TableType Type
        {
            get
            {
                return TableType.QualifiedJoin;
            }
        }

        internal SqlQualifiedJoinedTable(SqlStatement parentStatement, SqlParser.QualifiedJoinRefContext fieldNode, string source)
        {
            mSource = source;
            mParentStatement = parentStatement;

            LeftTable = SqlFromClause.BuildTableReference(parentStatement, fieldNode.tableReference(), source);

            SqlParser.JoinTypeContext joinType = fieldNode.joinType();
            // A bare JOIN (no explicit type) defaults to INNER; see KNOWN_BUGS.md #3.
            JoinType = joinType == null ? "INNER"
                : (joinType.outerJoinType() != null ? joinType.outerJoinType().GetText() : "INNER");

            RightTable = new SqlPrimaryTable(parentStatement, fieldNode.tablePrimary(), source);

            mExpressionNode = fieldNode.joinCondition().expr();
        }
    }
}

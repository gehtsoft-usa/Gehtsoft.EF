using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlQualifiedJoinedTable : SqlTableSpecification
    {
        private SqlBaseExpression mJoinCondition;
        private ASTNode? mExpressionNode;
        private readonly SqlStatement mParentStatement;
        private readonly string mSource;

        internal QueryBuilderEntity BuilderEntity { get; set; }

        internal void TryExpression()
        {
            if (mExpressionNode.HasValue)
            {
                mJoinCondition = SqlExpressionParser.ParseExpression(mParentStatement, mExpressionNode.Value, mSource);
                if (mJoinCondition == null)
                {
                    throw new SqlParserException(new SqlError(mSource,
                        mExpressionNode.Value.Position.Line,
                        mExpressionNode.Value.Position.Column,
                        $"Unexpected or incorrect expression node {mExpressionNode.Value.Symbol.Name}({mExpressionNode.Value.Value ?? "null"})"));
                }
                if (mJoinCondition.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                {
                    throw new SqlParserException(new SqlError(mSource,
                        mExpressionNode.Value.Position.Line,
                        mExpressionNode.Value.Position.Column,
                        $"Result of ON should be boolean {mExpressionNode.Value.Symbol.Name} ({mExpressionNode.Value.Value ?? "null"})"));
                }
                if (Statement.HasAggregateFunctions(mJoinCondition))
                {
                    throw new SqlParserException(new SqlError(mSource,
                        mExpressionNode.Value.Position.Line,
                        mExpressionNode.Value.Position.Column,
                        $"ON expression should not contain calls of aggregate functions ({mExpressionNode.Value.Value ?? "null"})"));
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

        internal SqlQualifiedJoinedTable(SqlStatement parentStatement, ASTNode fieldNode, string source)
        {
            mSource = source;
            mParentStatement = parentStatement;

            ASTNode node1 = fieldNode.Children[0];
            ASTNode node2 = fieldNode.Children[1];
            ASTNode node3 = fieldNode.Children[2];
            ASTNode node4 = fieldNode.Children[3];

            if (node1.Symbol.ID == SqlParser.ID.VariableTablePrimary)
            {
                LeftTable = new SqlPrimaryTable(parentStatement, node1, source);
            }
            else if (node1.Symbol.ID == SqlParser.ID.VariableQualifiedJoin)
            {
                LeftTable = new SqlQualifiedJoinedTable(parentStatement, node1, source);
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    node1.Position.Line,
                    node1.Position.Column,
                    $"Unexpected table reference node {node1.Symbol.Name}({node1.Value ?? "null"})"));
            }

            JoinType = node2.Value;

            if (node3.Symbol.ID == SqlParser.ID.VariableTablePrimary)
            {
                RightTable = new SqlPrimaryTable(parentStatement, node3, source);
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    node3.Position.Line,
                    node3.Position.Column,
                    $"Unexpected table reference node {node3.Symbol.Name}({node3.Value ?? "null"})"));
            }

            if (node4.Symbol.ID == SqlParser.ID.VariableJoinSpecification)
            {
                if (node4.Children[0].Symbol.ID == SqlParser.ID.VariableJoinCondition)
                {
                    mExpressionNode = node4.Children[0].Children[0];
                }
            }
        }

        internal SqlQualifiedJoinedTable(SqlTableSpecification leftTable, SqlPrimaryTable rightTable, string joinType, SqlBaseExpression joinCondition)
        {
            LeftTable = leftTable;
            RightTable = rightTable;
            JoinType = joinType;
            mJoinCondition = joinCondition;
        }
    }
}

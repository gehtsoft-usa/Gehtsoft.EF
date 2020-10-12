using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlAutoJoinedTable : SqlTableSpecification
    {
        private SqlTableSpecification mLeftTable;
        private SqlPrimaryTable mRightTable;

        internal override TableType Type
        {
            get
            {
                return TableType.QualifiedJoin;
            }
        }

        internal SqlTableSpecification LeftTable
        {
            get
            {
                return mLeftTable;
            }
        }

        internal SqlPrimaryTable RightTable
        {
            get
            {
                return mRightTable;
            }
        }

        internal SqlAutoJoinedTable(SqlStatement parentStatement, ASTNode fieldNode, string source)
        {
            ASTNode node1 = fieldNode.Children[0];
            ASTNode node2 = fieldNode.Children[1];

            if (node1.Symbol.ID == SqlParser.ID.VariableTablePrimary)
            {
                mLeftTable = new SqlPrimaryTable(parentStatement, node1, source);
            }
            else if (node1.Symbol.ID == SqlParser.ID.VariableQualifiedJoin)
            {
                mLeftTable = new SqlQualifiedJoinedTable(parentStatement, node1, source);
            }
            else if (node1.Symbol.ID == SqlParser.ID.VariableAutoJoin)
            {
                mLeftTable = new SqlAutoJoinedTable(parentStatement, node1, source);
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    node1.Position.Line,
                    node1.Position.Column,
                    $"Unexpected table reference node {node1.Symbol.Name}({node1.Value ?? "null"})"));
            }

            if (node2.Symbol.ID == SqlParser.ID.VariableTablePrimary)
            {
                mRightTable = new SqlPrimaryTable(parentStatement, node2, source);
            }
            else
            {
                throw new SqlParserException(new SqlError(source,
                    node2.Position.Line,
                    node2.Position.Column,
                    $"Unexpected table reference node {node2.Symbol.Name}({node2.Value ?? "null"})"));
            }
        }

        internal SqlAutoJoinedTable(SqlTableSpecification leftTable, SqlPrimaryTable rightTable)
        {
            mLeftTable = leftTable;
            mRightTable = rightTable;
        }

        internal virtual bool Equals(SqlAutoJoinedTable other)
        {
            if (other == null)
                return false;
            return (
                this.LeftTable.Equals(other.LeftTable) &&
                this.RightTable.Equals(other.RightTable)
            );
        }

        internal override bool Equals(SqlTableSpecification obj)
        {
            if (obj is SqlAutoJoinedTable item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
}

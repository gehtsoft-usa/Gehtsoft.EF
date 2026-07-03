using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlAutoJoinedTable : SqlTableSpecification
    {
        internal override TableType Type
        {
            get
            {
                return TableType.QualifiedJoin;
            }
        }

        internal SqlTableSpecification LeftTable { get; }

        internal SqlPrimaryTable RightTable { get; }

        internal SqlAutoJoinedTable(SqlStatement parentStatement, SqlParser.AutoJoinRefContext fieldNode, string source)
        {
            LeftTable = SqlFromClause.BuildTableReference(parentStatement, fieldNode.tableReference(), source);
            RightTable = new SqlPrimaryTable(parentStatement, fieldNode.tablePrimary(), source);
        }
    }
}

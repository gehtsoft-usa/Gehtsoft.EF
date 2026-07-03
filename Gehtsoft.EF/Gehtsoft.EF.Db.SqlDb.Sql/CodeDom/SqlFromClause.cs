namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlFromClause
    {
        internal SqlTableSpecificationCollection TableCollection { get; } = null;

        internal SqlFromClause(SqlStatement parentStatement, SqlParser.FromClauseContext statementNode, string source)
        {
            TableCollection = new SqlTableSpecificationCollection();
            foreach (SqlParser.TableReferenceContext tableReferenceNode in statementNode.tableReferenceList().tableReference())
            {
                TableCollection.Add(BuildTableReference(parentStatement, tableReferenceNode, source));
            }
        }

        internal SqlFromClause(SqlTableSpecificationCollection tableCollection)
        {
            TableCollection = tableCollection;
        }

        /// <summary>
        /// Builds a table specification from a (possibly left-recursive) table reference.
        /// Shared by <see cref="SqlFromClause"/> and the join classes, whose left side is
        /// itself a table reference.
        /// </summary>
        internal static SqlTableSpecification BuildTableReference(SqlStatement parentStatement, SqlParser.TableReferenceContext node, string source)
        {
            switch (node)
            {
                case SqlParser.PrimaryTableRefContext c:
                    return new SqlPrimaryTable(parentStatement, c.tablePrimary(), source);
                case SqlParser.QualifiedJoinRefContext c:
                    return new SqlQualifiedJoinedTable(parentStatement, c, source);
                case SqlParser.AutoJoinRefContext c:
                    return new SqlAutoJoinedTable(parentStatement, c, source);
            }
            throw new SqlParserException(new SqlError(source,
                node.Line(),
                node.Col(),
                $"Unexpected table reference ({node.GetText()})"));
        }
    }
}

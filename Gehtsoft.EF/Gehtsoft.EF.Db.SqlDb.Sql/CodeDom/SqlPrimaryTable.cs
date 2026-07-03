namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlPrimaryTable : SqlTableSpecification
    {
        internal string TableName { get; }
        internal string CorrelationName { get; }
        internal override TableType Type
        {
            get
            {
                return TableType.Primary;
            }
        }

        internal SqlPrimaryTable(SqlStatement parentStatement, SqlParser.TablePrimaryContext fieldNode, string source)
        {
            if (fieldNode.IDENTIFIER().Length > 1)
            {
                TableName = fieldNode.IDENTIFIER(0).GetText();
                CorrelationName = fieldNode.IDENTIFIER(1).GetText();
            }
            else
            {
                TableName = fieldNode.IDENTIFIER(0).GetText();
            }

            try
            {
                parentStatement.AddEntityEntry(TableName, CorrelationName);
            }
            catch
            {
                throw new SqlParserException(new SqlError(source,
                    fieldNode.Line(),
                    fieldNode.Col(),
                    $"Not found entity with name '{TableName}'"));
            }
        }
    }
}

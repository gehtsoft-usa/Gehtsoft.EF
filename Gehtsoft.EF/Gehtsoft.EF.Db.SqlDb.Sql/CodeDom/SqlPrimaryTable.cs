using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        internal SqlPrimaryTable(SqlStatement parentStatement, ASTNode fieldNode, string source)
        {
            if (fieldNode.Children.Count > 1)
            {
                TableName = fieldNode.Children[0].Value;
                CorrelationName = fieldNode.Children[1].Value;
            }
            else
            {
                TableName = fieldNode.Children[0].Value;
            }

            try
            {
                parentStatement.AddEntityEntry(TableName, CorrelationName);
            }
            catch
            {
                throw new SqlParserException(new SqlError(source,
                    fieldNode.Position.Line,
                    fieldNode.Position.Column,
                    $"Not found entity with name '{TableName}'"));
            }
        }
    }
}

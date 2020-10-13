using Hime.Redist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SqlWhereClause
    {

        internal SqlBaseExpression RootExpression { get; set; }

        internal SqlWhereClause(SqlStatement parentStatement, ASTNode statementNode, string source)
        {
            RootExpression = SqlExpressionParser.ParseExpression(parentStatement, statementNode.Children[0], source);
            if (RootExpression == null)
            {
                throw new SqlParserException(new SqlError(source,
                    statementNode.Position.Line,
                    statementNode.Position.Column,
                    $"Unexpected or incorrect expression node {statementNode.Symbol.Name}({statementNode.Value ?? "null"})"));
            }
        }
    }
}

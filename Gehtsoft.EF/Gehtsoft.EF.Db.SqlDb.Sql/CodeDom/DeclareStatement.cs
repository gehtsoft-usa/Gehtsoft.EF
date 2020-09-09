using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class DeclareStatement : Statement
    {
        internal DeclareStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Declare)
        {
            foreach (ASTNode node in statementNode.Children[0].Children)
            {
                string name = $"?{node.Children[0].Value}";
                ResultTypes resultType = ResultTypes.Unknown;
                switch (node.Children[1].Value)
                {
                    case "STRING":
                        resultType = ResultTypes.String;
                        break;
                    case "INTEGER":
                        resultType = ResultTypes.Integer;
                        break;
                    case "DOUBLE":
                        resultType = ResultTypes.Double;
                        break;
                    case "BOOLEAN":
                        resultType = ResultTypes.Boolean;
                        break;
                    case "DATETIME":
                        resultType = ResultTypes.DateTime;
                        break;
                }

                if(!builder.AddGlobalParameter(name, resultType))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        node.Children[0].Position.Line,
                        node.Children[0].Position.Column,
                        $"Duplicate declared name ({name})"));
                }
            }
        }
    }
}

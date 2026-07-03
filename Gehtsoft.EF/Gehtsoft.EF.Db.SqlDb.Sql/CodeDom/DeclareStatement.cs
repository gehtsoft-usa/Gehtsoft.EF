using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class DeclareStatement : Statement
    {
        private readonly Dictionary<string, ResultTypes> variables = new Dictionary<string, ResultTypes>();
        internal DeclareStatement(SqlCodeDomBuilder builder, SqlParser.DeclareStatementContext statementNode, string currentSource)
            : base(builder, StatementType.Declare)
        {
            foreach (SqlParser.DeclareItemContext node in statementNode.declareList().declareItem())
            {
                string name = $"?{node.IDENTIFIER().GetText()}";
                ResultTypes resultType = GetResultTypeByName(node.parameterType().GetText());

                if (!builder.AddGlobalParameter(name, resultType))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        node.Line(),
                        node.Col(),
                        $"Duplicate declared name ({name})"));
                }

                variables[name] = resultType;
            }
        }

        internal void Run()
        {
            foreach (KeyValuePair<string, ResultTypes> item in variables)
            {
                if (!CodeDomBuilder.AddGlobalParameter(item.Key, item.Value))
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Duplicate declared name ({item.Key})"));
                }
            }
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null);
        }
    }
}

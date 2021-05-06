using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class ImportStatement : Statement
    {
        private readonly Dictionary<string, ResultTypes> variables = new Dictionary<string, ResultTypes>();
        internal ImportStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Import)
        {
            foreach (ASTNode node in statementNode.Children[0].Children)
            {
                string name = $"?{node.Children[0].Value}";
                ResultTypes resultType = GetResultTypeByName(node.Children[1].Value);

                if (!builder.AddGlobalParameter(name, resultType))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        node.Children[0].Position.Line,
                        node.Children[0].Position.Column,
                        $"Duplicate imported name ({name})"));
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
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Duplicate imported name ({item.Key})"));
                }
                //new SqlConstant(null, item.Value).SystemType)
                string name = item.Key.Substring(1);
                IDictionary<string, object> dict = this.CodeDomBuilder.ParametersDictionary;
                if (dict == null)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, "No parameters list in call"));
                }
                if (!dict.ContainsKey(name))
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"No parameter with name ({name}) in parameters list"));
                }
                object result = null;
                try
                {
                    result = Convert.ChangeType(dict[name], SqlBaseExpression.GetSystemType(item.Value));
                }
                catch { }
                if (result == null)
                {
                    throw new SqlParserException(new SqlError(null, 0, 0, $"Parameter with name ({name}) can not be converted to '{item.Value}'"));
                }
                CodeDomBuilder.UpdateGlobalParameter(item.Key, new SqlConstant(result, item.Value));
            }
        }

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(Expression.Constant(this), "Run", null);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class ExitStatement : Statement
    {
        internal SqlBaseExpression ExitExpression { get; } = null;

        internal ExitStatement(SqlCodeDomBuilder builder, SqlParser.ExitStatementContext statementNode, string currentSource)
            : base(builder, StatementType.Exit)
        {
            if (statementNode.expr().Length > 0)
            {
                SqlParser.ExprContext expressionNode = statementNode.expr(0);
                ExitExpression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
                if (!Statement.IsCalculable(ExitExpression))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        expressionNode.Line(),
                        expressionNode.Col(),
                        "Not calculable expression in EXIT statement"));
                }
                if (ExitExpression.ResultType == SqlBaseExpression.ResultTypes.Cursor)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        expressionNode.Line(),
                        expressionNode.Col(),
                        "Cursor expression can not be used in EXIT statement"));
                }
            }
        }

        internal override Expression ToLinqWxpression()
        {
            var array = CodeDomBuilder.BlockDescriptors.ToArray();
            BlockDescriptor descr = array[array.Length - 1];
            return Expression.Block(
                Expression.Call(Expression.Constant(CodeDomBuilder), "ExitRun", null, Expression.Constant(this.ExitExpression)),
                Expression.Goto(descr.EndLabel)
            );
        }
    }
}

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
    internal class SwitchStatement : Statement
    {
        internal ConditionalStatementsRunCollection ConditionalRuns { get; }
        internal SwitchStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Switch)
        {
            ConditionalRuns = new ConditionalStatementsRunCollection();
            SqlBaseExpression leftOperand = SqlExpressionParser.ParseExpression(this, statementNode.Children[0], currentSource);
            if (!Statement.IsCalculable(leftOperand))
            {
                throw new SqlParserException(new SqlError(currentSource,
                    statementNode.Children[0].Position.Line,
                    statementNode.Children[0].Position.Column,
                    $"Not calculable expression in SWITCH statement"));
            }
            ConditionalStatementsRun conditionalRun = null;

            for (int i = 1; i < statementNode.Children.Count; i++)
            {
                ASTNode node = statementNode.Children[i];
                if (node.Symbol.ID == SqlParser.ID.VariableRoot)
                {
                    if (conditionalRun == null)
                    {
                        conditionalRun = new ConditionalStatementsRun(new SqlConstant(true, ResultTypes.Boolean));
                    }
                    conditionalRun.LinqExpression = builder.ParseNodeToLinq("SWITCH CASE", node, this);
                    ConditionalRuns.Add(conditionalRun);
                    conditionalRun = null;
                }
                else
                {
                    SqlBaseExpression rightOperand = SqlExpressionParser.ParseExpression(this, node, currentSource);
                    if (!Statement.IsCalculable(rightOperand))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Not calculable expression in CASE statement"));
                    }
                    if (rightOperand.ResultType != leftOperand.ResultType)
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Type of CASE ({rightOperand.ResultType}) doesn't match type of SWITCH ({leftOperand.ResultType})"));
                    }
                    if (conditionalRun != null)
                    {
                        conditionalRun.ConditionalExpression = new SqlBinaryExpression(conditionalRun.ConditionalExpression,
                            SqlBinaryExpression.OperationType.Or,
                            new SqlBinaryExpression(leftOperand, SqlBinaryExpression.OperationType.Eq, rightOperand));
                    }
                    else
                    {
                        conditionalRun = new ConditionalStatementsRun(new SqlBinaryExpression(leftOperand, SqlBinaryExpression.OperationType.Eq, rightOperand));
                    }
                }
            }
        }

        internal SwitchStatement(SqlCodeDomBuilder builder, ConditionalStatementsRunCollection conditionalRuns)
            : base(builder, StatementType.Switch)
        {
            ConditionalRuns = conditionalRuns;
        }

        internal override Expression ToLinqWxpression()
        {
            List<SwitchCase> cases = new List<SwitchCase>();
            foreach (ConditionalStatementsRun item in ConditionalRuns)
            {
                cases.Add(Expression.SwitchCase(item.LinqExpression, StatementRunner.CalculateExpressionValue<bool>(item.ConditionalExpression, CodeDomBuilder)));
            }
            ConstantExpression switchValue = Expression.Constant(true);
            return Expression.Switch(
                switchValue,
                Expression.Constant(null),
                cases.ToArray()
            );
        }
    }
}

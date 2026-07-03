using Antlr4.Runtime.Tree;
using System.Collections.Generic;
using System.Linq.Expressions;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class SwitchStatement : Statement
    {
        internal ConditionalStatementsRunCollection ConditionalRuns { get; }
        internal SwitchStatement(SqlCodeDomBuilder builder, SqlParser.SwitchStatementContext statementNode, string currentSource)
            : base(builder, StatementType.Switch)
        {
            ConditionalRuns = new ConditionalStatementsRunCollection();
            SqlBaseExpression leftOperand = null;
            ConditionalStatementsRun conditionalRun = null;

            // Walk the parse-tree children in order. The first expr is the SWITCH operand;
            // subsequent exprs are CASE values (OR-combined until a body closes the run);
            // each statementList is a CASE/OTHERWISE body. Keyword/punctuation terminals are skipped.
            foreach (IParseTree child in statementNode.children)
            {
                if (child is SqlParser.ExprContext exprNode)
                {
                    if (leftOperand == null)
                    {
                        leftOperand = SqlExpressionParser.ParseExpression(this, exprNode, currentSource);
                        if (!Statement.IsCalculable(leftOperand))
                        {
                            throw new SqlParserException(new SqlError(currentSource,
                                exprNode.Line(), exprNode.Col(),
                                "Not calculable expression in SWITCH statement"));
                        }
                        continue;
                    }

                    SqlBaseExpression rightOperand = SqlExpressionParser.ParseExpression(this, exprNode, currentSource);
                    if (!Statement.IsCalculable(rightOperand))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            exprNode.Line(), exprNode.Col(),
                            "Not calculable expression in CASE statement"));
                    }
                    if (rightOperand.ResultType != leftOperand.ResultType)
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            exprNode.Line(), exprNode.Col(),
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
                else if (child is SqlParser.StatementListContext bodyNode)
                {
                    if (conditionalRun == null)
                    {
                        conditionalRun = new ConditionalStatementsRun(new SqlConstant(true, ResultTypes.Boolean));
                    }
                    conditionalRun.LinqExpression = builder.ParseNodeToLinq("SWITCH CASE", bodyNode, this);
                    ConditionalRuns.Add(conditionalRun);
                    conditionalRun = null;
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

using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using static Gehtsoft.EF.Db.SqlDb.Sql.CodeDom.SqlBaseExpression;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal class IfStatement : Statement
    {
        internal ConditionalStatementsRunCollection ConditionalRuns { get; }
        internal IfStatement(SqlCodeDomBuilder builder, SqlParser.IfStatementContext statementNode, string currentSource)
            : base(builder, StatementType.If)
        {
            ConditionalRuns = new ConditionalStatementsRunCollection();

            // IF cond THEN body (ELSIF cond THEN body)* (ELSE body)? — condition[i] pairs with body[i];
            // an ELSE body has no matching condition and gets an implicit `true`.
            int conditionCount = statementNode.expr().Length;
            for (int i = 0; i < conditionCount; i++)
            {
                SqlParser.ExprContext node = statementNode.expr(i);
                SqlBaseExpression conditionalExpression = SqlExpressionParser.ParseExpression(this, node, currentSource);
                if (!Statement.IsCalculable(conditionalExpression))
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        node.Line(), node.Col(),
                        "Not calculable expression in IF statement"));
                }
                if (conditionalExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                {
                    throw new SqlParserException(new SqlError(currentSource,
                        node.Line(), node.Col(),
                        $"Condition expression of IF(ELSIF) should be boolean ({node.GetText()})"));
                }
                ConditionalStatementsRun run = new ConditionalStatementsRun(conditionalExpression)
                {
                    LinqExpression = builder.ParseNodeToLinq("IF-ELSE Body", statementNode.statementList(i), this)
                };
                ConditionalRuns.Add(run);
            }

            if (statementNode.statementList().Length > conditionCount)
            {
                ConditionalStatementsRun elseRun = new ConditionalStatementsRun(new SqlConstant(true, ResultTypes.Boolean))
                {
                    LinqExpression = builder.ParseNodeToLinq("IF-ELSE Body", statementNode.statementList(conditionCount), this)
                };
                ConditionalRuns.Add(elseRun);
            }
        }

        internal IfStatement(SqlCodeDomBuilder builder, ConditionalStatementsRunCollection conditionalRuns)
            : base(builder, StatementType.If)
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
    internal class ConditionalStatementsRun
    {
        internal Expression LinqExpression { get; set; }
        internal SqlBaseExpression ConditionalExpression { get; set; }

        internal ConditionalStatementsRun(SqlBaseExpression conditionalExpression)
        {
            ConditionalExpression = conditionalExpression;
        }
    }

    internal class ConditionalStatementsRunCollection : IReadOnlyList<ConditionalStatementsRun>
    {
        private readonly List<ConditionalStatementsRun> mList = new List<ConditionalStatementsRun>();

        internal ConditionalStatementsRunCollection()
        {
        }

        public ConditionalStatementsRun this[int index] => ((IReadOnlyList<ConditionalStatementsRun>)mList)[index];

        public int Count => ((IReadOnlyCollection<ConditionalStatementsRun>)mList).Count;

        public IEnumerator<ConditionalStatementsRun> GetEnumerator()
        {
            return ((IEnumerable<ConditionalStatementsRun>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(ConditionalStatementsRun conditionalRun)
        {
            mList.Add(conditionalRun);
        }
    }
}

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
    internal class IfStatement : Statement
    {
        internal ConditionalStatementsRunCollection ConditionalRuns { get; }
        internal IfStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.If)
        {
            ConditionalRuns = new ConditionalStatementsRunCollection();
            ConditionalStatementsRun currentConditionalRun = null;
            foreach (ASTNode node in statementNode.Children)
            {
                if (node.Symbol.ID == SqlParser.ID.VariableRoot)
                {
                    if (currentConditionalRun == null)
                    {
                        currentConditionalRun = new ConditionalStatementsRun(new SqlConstant(true, ResultTypes.Boolean));
                    }
                    currentConditionalRun.LinqExpression = builder.ParseNodeToLinq("IF-ELSE Body", node, this);
                    ConditionalRuns.Add(currentConditionalRun);
                    currentConditionalRun = null;
                }
                else
                {
                    if (currentConditionalRun != null)
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Unexpected condition expression in IF statement {node.Symbol.Name} ({node.Value ?? "null"})"));
                    }
                    SqlBaseExpression conditionalExpression = SqlExpressionParser.ParseExpression(this, node, currentSource);
                    if (!Statement.IsCalculable(conditionalExpression))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            "Not calculable expression in IF statement"));
                    }
                    if (conditionalExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Condition expression of IF(ELSIF) should be boolean {node.Symbol.Name} ({node.Value ?? "null"})"));
                    }
                    currentConditionalRun = new ConditionalStatementsRun(conditionalExpression);
                }
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

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
    public class IfStatement : Statement
    {
        public ConditionalStatementsRunCollection ConditionalRuns { get; }
        internal IfStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.If)
        {
            ConditionalRuns = new ConditionalStatementsRunCollection();
            ConditionalStatementsRun currentConditionalRun = null;
            foreach (ASTNode node in statementNode.Children)
            {
                if(node.Symbol.ID == SqlParser.ID.VariableRoot)
                {
                    StatementSetEnvironment inner = builder.ParseNode("IF-ELSE Body", node, this);
                    if (currentConditionalRun == null)
                    {
                        currentConditionalRun = new ConditionalStatementsRun(new SqlConstant(true, ResultTypes.Boolean));
                    }
                    currentConditionalRun.Statements = inner;
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
                    if(!Statement.IsCalculable(conditionalExpression))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Not calculable expression in IF statement"));
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

        public virtual bool Equals(IfStatement other)
        {
            if (other is IfStatement stmt)
            {
                if (ConditionalRuns == null && stmt.ConditionalRuns != null)
                    return false;
                if (ConditionalRuns != null && !ConditionalRuns.Equals(stmt.ConditionalRuns))
                    return false;
                return true;
            }
            return base.Equals(other);
        }

        public override bool Equals(Statement obj)
        {
            if (obj is IfStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }
    public class ConditionalStatementsRun : IEquatable<ConditionalStatementsRun>
    {
        public StatementSetEnvironment Statements { get; internal set; }
        public SqlBaseExpression ConditionalExpression { get; internal set; }

        internal ConditionalStatementsRun(SqlBaseExpression conditionalExpression, StatementSetEnvironment statements = null)
        {
            Statements = statements;
            ConditionalExpression = conditionalExpression;
        }

        public virtual bool Equals(ConditionalStatementsRun other)
        {
            if (other == null)
                return false;
            return this.ConditionalExpression.Equals(other.ConditionalExpression) && this.Statements.Equals(other.Statements);
        }

        public override bool Equals(object obj)
        {
            if (obj is ConditionalStatementsRun item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class ConditionalStatementsRunCollection : IReadOnlyList<ConditionalStatementsRun>, IEquatable<ConditionalStatementsRunCollection>
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

        public virtual bool Equals(ConditionalStatementsRunCollection other)
        {
            if (other == null)
                return false;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Count != other.Count)
                return false;
            for (int i = 0; i < Count; i++)
            {
                if (!this[i].Equals(other[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is ConditionalStatementsRunCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}

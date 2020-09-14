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
        public IfItemCollection IfItems { get; }
        internal IfStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.If)
        {
            IfItems = new IfItemCollection();
            IfItem currentIfItem = null;
            foreach (ASTNode node in statementNode.Children)
            {
                if(node.Symbol.ID == SqlParser.ID.VariableRoot)
                {
                    StatementSetEnvironment inner = builder.ParseNode("IF-ELSE Body", node, this);
                    if (currentIfItem == null)
                    {
                        currentIfItem = new IfItem(new SqlConstant(true, ResultTypes.Boolean));
                    }
                    currentIfItem.Statements = inner;
                    IfItems.Add(currentIfItem);
                    currentIfItem = null;
                }
                else
                {
                    if (currentIfItem != null)
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Unexpected condition expression in IF statement {node.Symbol.Name} ({node.Value ?? "null"})"));
                    }
                    SqlBaseExpression ifExpression = SqlExpressionParser.ParseExpression(this, node, currentSource);
                    if (ifExpression.ResultType != SqlBaseExpression.ResultTypes.Boolean)
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            node.Position.Line,
                            node.Position.Column,
                            $"Condition expression of IF(ELSIF) should be boolean {node.Symbol.Name} ({node.Value ?? "null"})"));
                    }
                    currentIfItem = new IfItem(ifExpression);
                }
            }
        }

        internal IfStatement(SqlCodeDomBuilder builder, IfItemCollection ifItems)
            : base(builder, StatementType.If)
        {
            IfItems = ifItems;
        }

        public virtual bool Equals(IfStatement other)
        {
            if (other is IfStatement stmt)
            {
                if (IfItems == null && stmt.IfItems != null)
                    return false;
                if (IfItems != null && !IfItems.Equals(stmt.IfItems))
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
    public class IfItem : IEquatable<IfItem>
    {
        public StatementSetEnvironment Statements { get; internal set; }
        public SqlBaseExpression IfExpression { get; }

        internal IfItem(SqlBaseExpression ifExpression, StatementSetEnvironment statements = null)
        {
            Statements = statements;
            IfExpression = ifExpression;
        }

        public virtual bool Equals(IfItem other)
        {
            if (other == null)
                return false;
            return this.IfExpression.Equals(other.IfExpression) && this.Statements.Equals(other.Statements);
        }

        public override bool Equals(object obj)
        {
            if (obj is IfItem item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class IfItemCollection : IReadOnlyList<IfItem>, IEquatable<IfItemCollection>
    {
        private readonly List<IfItem> mList = new List<IfItem>();

        internal IfItemCollection()
        {

        }

        public IfItem this[int index] => ((IReadOnlyList<IfItem>)mList)[index];

        public int Count => ((IReadOnlyCollection<IfItem>)mList).Count;

        public IEnumerator<IfItem> GetEnumerator()
        {
            return ((IEnumerable<IfItem>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(IfItem fieldName)
        {
            mList.Add(fieldName);
        }

        public virtual bool Equals(IfItemCollection other)
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
            if (obj is IfItemCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}

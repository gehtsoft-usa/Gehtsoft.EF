using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    public class SetStatement : Statement
    {
        public SetItemCollection SetItems { get; }

        internal SetStatement(SqlCodeDomBuilder builder, ASTNode statementNode, string currentSource)
            : base(builder, StatementType.Set)
        {
            SetItems = new SetItemCollection();
            foreach (ASTNode node in statementNode.Children[0].Children)
            {
                string name = node.Children[0].Value;
                SqlBaseExpression existing = builder.FindGlobalParameter($"?{name}");
                if (node.Children.Count > 1)
                {
                    ASTNode expressionNode = node.Children[1];
                    SqlBaseExpression expression = SqlExpressionParser.ParseExpression(this, expressionNode, currentSource);
                    if(!Statement.IsCalculable(expression))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            expressionNode.Position.Line,
                            expressionNode.Position.Column,
                            $"Not calculable expression in SET statement ({expressionNode.Value ?? "null"})"));
                    }
                    if(existing != null)
                    {
                        if(existing.ResultType != expression.ResultType)
                        {
                            throw new SqlParserException(new SqlError(currentSource,
                                expressionNode.Position.Line,
                                expressionNode.Position.Column,
                                $"Expression in SET statement doesn't match type of declared ({expressionNode.Value ?? "null"})"));
                        }
                    }
                    SetItems.Add(new SetItem(name, expression));
                }
                else
                {
                    SetItems.Add(new SetItem(name));
                }
            }
        }

        internal SetStatement(SqlCodeDomBuilder builder, SetItemCollection setItems)
            : base(builder, StatementType.Set)
        {
            SetItems = setItems;
        }
    }

    public class SetItem : IEquatable<SetItem>
    {
        public string Name { get; }
        public SqlBaseExpression Expression { get; }

        internal SetItem(string name, SqlBaseExpression expression)
        {
            Name = name;
            Expression = expression;
        }

        internal SetItem(string name)
        {
            Name = name;
            Expression = new SqlConstant(null, SqlBaseExpression.ResultTypes.Unknown);
        }

        public virtual bool Equals(SetItem other)
        {
            if (other == null)
                return false;
            return (this.Expression.Equals(other.Expression) && this.Name == other.Name);
        }

        public override bool Equals(object obj)
        {
            if (obj is SetItem item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    public class SetItemCollection : IReadOnlyList<SetItem>, IEquatable<SetItemCollection>
    {
        private readonly List<SetItem> mList = new List<SetItem>();

        internal SetItemCollection()
        {

        }

        public SetItem FindByName(string name) => mList.Where(t => t.Name == name).SingleOrDefault();

        public SetItem this[int index] => ((IReadOnlyList<SetItem>)mList)[index];

        public int Count => ((IReadOnlyCollection<SetItem>)mList).Count;

        public IEnumerator<SetItem> GetEnumerator()
        {
            return ((IEnumerable<SetItem>)mList).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)mList).GetEnumerator();
        }

        internal void Add(SetItem fieldName)
        {
            mList.Add(fieldName);
        }

        public virtual bool Equals(SetItemCollection other)
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
            if (obj is SetItemCollection item)
                return Equals(item);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

    }
}

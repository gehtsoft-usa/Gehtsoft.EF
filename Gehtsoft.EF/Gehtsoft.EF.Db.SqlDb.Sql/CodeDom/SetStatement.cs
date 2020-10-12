using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Hime.Redist;

namespace Gehtsoft.EF.Db.SqlDb.Sql.CodeDom
{
    internal  class SetStatement : Statement
    {
        internal  SetItemCollection SetItems { get; }

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
                    if (!Statement.IsCalculable(expression))
                    {
                        throw new SqlParserException(new SqlError(currentSource,
                            expressionNode.Position.Line,
                            expressionNode.Position.Column,
                            $"Not calculable expression in SET statement ({expressionNode.Value ?? "null"})"));
                    }
                    if (existing != null)
                    {
                        if (existing.ResultType != expression.ResultType)
                        {
                            throw new SqlParserException(new SqlError(currentSource,
                                expressionNode.Position.Line,
                                expressionNode.Position.Column,
                                $"Expression in SET statement doesn't match type of declared before ({existing.ResultType.ToString()})"));
                        }
                    }
                    else
                    {
                        builder.AddGlobalParameter($"?{name}", expression.ResultType);
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

        internal override Expression ToLinqWxpression()
        {
            return Expression.Call(typeof(SetRunner), "Run", null,
                Expression.Constant(CodeDomBuilder), Expression.Constant(this)
                );
        }

        internal  virtual bool Equals(SetStatement other)
        {
            if (other is SetStatement stmt)
            {
                if (SetItems == null && stmt.SetItems != null)
                    return false;
                if (SetItems != null && !SetItems.Equals(stmt.SetItems))
                    return false;
                return true;
            }
            return base.Equals(other);
        }

        internal override bool Equals(Statement obj)
        {
            if (obj is SetStatement item)
                return Equals(item);
            return base.Equals(obj);
        }
    }

    internal  class SetItem : IEquatable<SetItem>
    {
        internal  string Name { get; }
        internal  SqlBaseExpression Expression { get; }

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

        bool IEquatable<SetItem>.Equals(SetItem other) => Equals(other);
        internal virtual bool Equals(SetItem other)
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

    internal  class SetItemCollection : IReadOnlyList<SetItem>, IEquatable<SetItemCollection>
    {
        private readonly List<SetItem> mList = new List<SetItem>();

        internal SetItemCollection()
        {

        }

        internal  SetItem FindByName(string name) => mList.Where(t => t.Name == name).SingleOrDefault();

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

        bool IEquatable<SetItemCollection>.Equals(SetItemCollection other) => Equals(other);
        internal virtual bool Equals(SetItemCollection other)
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

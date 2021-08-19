using System;
using System.Collections;
using System.Collections.Generic;

namespace Gehtsoft.EF.Test.SqlParser
{
    public sealed class AstNodeImpl : IAstNode
    {
        public string Symbol { get; set;  }
        public string Value { get; set; }

        private readonly List<IAstNode> mChildren = new List<IAstNode>();

        private class ChildrenWrapper : IAstNodeChildren
        {
            private readonly List<IAstNode> mChildren;

            public ChildrenWrapper(List<IAstNode> children)
            {
                mChildren = children;
            }

            public IAstNode this[int index] => mChildren[index];

            public int Count => mChildren.Count;

            public IEnumerator<IAstNode> GetEnumerator() => mChildren.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => mChildren.GetEnumerator();
        }

        public IAstNodeChildren Children { get; } 

        public AstNodeImpl()
        {
            Children = new ChildrenWrapper(mChildren);
        }

        public AstNodeImpl(string symbol, string value) : this()
        {
            Symbol = symbol;
            Value = value;
        }

      
        public void Add(IAstNode node) => mChildren.Add(node);

        public override string ToString()
        {
            return this.ToAstText();
        }

        public string ToString(string format, IFormatProvider formatProvider) => ToString();

        public bool Equals(IAstNode other)
        {
            if (other == null)
                return false;
            return Symbol == other.Symbol && Value == other.Value;
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using Hime.Redist;

namespace Gehtsoft.EF.Test.SqlParser
{
    public sealed class AstNodeWrapper : IAstNode
    {
        private readonly ASTNode mNode;

        private class ChildrenWrapper : IAstNodeChildren
        {
            private readonly ASTNode mNode;
            private readonly AstNodeWrapper[] mWrappers;

            public ChildrenWrapper(ASTNode children)
            {
                mNode = children;
                mWrappers = new AstNodeWrapper[mNode.Children.Count];
            }

            public IAstNode this[int index] => mWrappers[index] ??= new AstNodeWrapper(mNode.Children[index]);

            public int Count => mNode.Children.Count;

            public IEnumerator<IAstNode> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                    yield return this[i];
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public IAstNodeChildren Children { get; }

        public AstNodeWrapper(ASTNode node)
        {
            mNode = node;
            Children = new ChildrenWrapper(node);
        }

        public string Symbol => mNode.Symbol.Name;

        public string Value => mNode.Value;

        public int Count => mNode.Children.Count;

        public override string ToString() => this.ToAstText();

        public string ToString(string format, IFormatProvider formatProvider) => ToString();

        public bool Equals(IAstNode other)
        {
            if (other == null)
                return false;
            return Symbol == other.Symbol && Value == other.Value;
        }
    }
}

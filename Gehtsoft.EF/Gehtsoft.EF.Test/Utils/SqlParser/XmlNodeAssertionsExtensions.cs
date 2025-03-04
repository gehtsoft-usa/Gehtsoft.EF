using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using DotLiquid.Exceptions;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using FluentAssertions.Xml;

namespace Gehtsoft.EF.Test.SqlParser
{
    public class AstNodeAssertions : ReferenceTypeAssertions<IAstNode, AstNodeAssertions>
    {
        private readonly AssertionChain mChain;

        public AstNodeAssertions(IAstNode node, AssertionChain chain) : base(node, chain)
        {
            mChain = chain;
        }

        protected override string Identifier => "node";

        public AndConstraint<AstNodeAssertions> Exist(string because = null, params object[] becauseArgs)
            => this.NotBeNull(because, becauseArgs);

        public AndConstraint<AstNodeAssertions> NotExist(string because = null, params object[] becauseArgs)
            => this.BeNull(because, becauseArgs);

        public AndConstraint<AstNodeAssertions> HaveSymbol(string symbol, string because = null, params object[] becauseArgs)
        {
            mChain
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node?.Symbol == symbol)
                .FailWith("Expected {context:node} to have a symbol {0} but it has {1}", symbol, Subject?.Symbol);
            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> HaveValue(string value, string because = null, params object[] becauseArgs)
        {
            mChain
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node?.Value == value)
                .FailWith("Expected {context:node} to have a value {0} but it has {1}", value, Subject?.Value);
            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> Contain(string path, string because = null, params object[] becauseArgs)
        {
            mChain
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node.SelectNode(path) != null)
                .FailWith("Expected {context:node} to contain a node at the path {0}, but it does not", path);
            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> ContainMatching(string path, Expression<Func<IAstNode, bool>> predicate, string because = null, params object[] becauseArgs)
        {
            mChain
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node.SelectNode(path) != null)
                .FailWith("Expected {context:node} to contain a node at the path {0}, but it does not", path)
                .Then
                .ForCondition(node => predicate.Compile()(node.SelectNode(path)))
                .FailWith("Expected {context:node} to contain a node at the path {0} and matching the predicate but node {1} does not match", path, Subject.SelectNode(path));

            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> NotContain(string path, string because = null, params object[] becauseArgs)
        {
            mChain
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => !node.Select(path).Any())
                .FailWith("Expected {context:node} to not contain node at the path {0} but it does have {1}", path, Subject.Select(path));
            return new AndConstraint<AstNodeAssertions>(this);
        }
    }
}

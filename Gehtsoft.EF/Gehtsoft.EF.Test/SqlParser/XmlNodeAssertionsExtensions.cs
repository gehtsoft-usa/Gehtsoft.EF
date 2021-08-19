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
        public AstNodeAssertions(IAstNode node) : base(node)
        {
        }

        protected override string Identifier => "node";



        public AndConstraint<AstNodeAssertions> Exists(string because = null, params object[] becauseArgs)
            => this.NotBeNull(because, becauseArgs);

        public AndConstraint<AstNodeAssertions> NotExists(string because = null, params object[] becauseArgs)
            => this.BeNull(because, becauseArgs);

        public AndConstraint<AstNodeAssertions> HaveSymbol(string symbol, string because = null, params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node.Symbol == symbol)
                .FailWith("Expected {context:node} to have a symbol {0}", symbol);
            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> HaveValue(string value, string because = null, params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node.Value == value)
                .FailWith("Expected {context:node} to have a value {0}", value);
            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> Contain(string path, string because = null, params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node.SelectNode(path) != null)
                .FailWith("Expected {context:node} to contain a node at the path {0}, but it does not", path);
            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> ContainMatching(string path, Expression<Func<IAstNode, bool>> predicate, string because = null, params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => node.SelectNode(path) != null)
                .FailWith("Expected {context:node} to contain a node at the path {0}, but it does not", path)
                .Then
                .ForCondition(node => predicate.Compile()(node.SelectNode(path)))
                .FailWith("Expected {context:node} to contain a node at the path {0} and matching the predicate {1} but node {2} does not match", path, predicate, Subject.SelectNode(path));

            return new AndConstraint<AstNodeAssertions>(this);
        }

        public AndConstraint<AstNodeAssertions> NotContain(string path, string because = null, params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .Given(() => Subject)
                .ForCondition(node => !node.Select(path).Any() )
                .FailWith("Expected {context:node} to not contain node at the path {0} but it does have {1}", path, Subject.Select(path));
            return new AndConstraint<AstNodeAssertions>(this);
        }
    }
}

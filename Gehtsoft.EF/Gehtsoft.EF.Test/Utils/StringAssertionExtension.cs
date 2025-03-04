using System;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Gehtsoft.EF.Test.Entity.Utils
{
    public static class StringAssertionExtension
    {
        public static AndConstraint<StringAssertions> Be(this StringAssertions assertions, string v, StringComparison comparison, string because = null, params object[] becauseParams)
        {
            assertions.CurrentAssertionChain
               .BecauseOf(because, becauseParams)
               .Given(() => assertions.Subject)
               .ForCondition(e => e?.Equals(v, comparison) ?? false)
               .FailWith("Expected {context:string} to be {1} but it is {0}", assertions.Subject, v);

            return new AndConstraint<StringAssertions>(assertions);
        }
    }
}

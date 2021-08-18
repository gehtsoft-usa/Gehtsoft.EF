using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Xml;

namespace Gehtsoft.EF.Test.SqlParser
{
    public static class XmlNodeAssertionsExtensions
    {
        public static AndConstraint<XmlNodeAssertions> Contain(this XmlNodeAssertions assertions, string xpath, string because = null, params string[] becauseArg)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArg)
                .Given(() => assertions.Subject)
                .ForCondition(n => n.SelectNodes(xpath).Count > 0)
                .FailWith("Expected {context:Xml Node} to contain children than matches {0} but it does not", xpath);

            return new AndConstraint<XmlNodeAssertions>(assertions);
        }
    }
}

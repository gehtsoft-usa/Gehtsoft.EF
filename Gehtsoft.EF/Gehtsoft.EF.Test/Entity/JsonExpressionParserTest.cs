using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Xunit;

namespace Gehtsoft.EF.Test.Entity
{
    /// <summary>
    /// The JSON path parser turns a member/index expression chain (e =&gt; e.Json.Field[i].Field) into a
    /// property name + a "$.path" string. These unit tests cover the list-indexer (get_Item) step and the
    /// two malformed-expression guards; the parser only inspects the expression shape, so no database or
    /// entity registration is needed.
    /// </summary>
    public class JsonExpressionParserTest
    {
        private class Leaf { public string Name { get; set; } }
        private class Mid { public List<Leaf> Items { get; set; } public string Title { get; set; } }
        private class Root { public Mid Json { get; set; } }

        [Fact]
        public void ListIndexer_BuildsJsonPath()
        {
            Expression<Func<Root, object>> e = r => r.Json.Items[2].Name;
            JsonExpressionParser.Parse(e, out string property, out string path, out Type valueType);

            property.Should().Be("Json");
            path.Should().Be("$.Items[2].Name");
            valueType.Should().Be(typeof(string));
        }

        [Fact]
        public void TooShortChain_Throws()
        {
            Expression<Func<Root, object>> e = r => r.Json; // only the property, no field beneath it
            ((Action)(() => JsonExpressionParser.Parse(e, out _, out _, out _)))
                .Should().Throw<ArgumentException>();
        }

        [Fact]
        public void NonConstantIndex_Throws()
        {
            int i = 1; // captured variable -> not a constant expression
            Expression<Func<Root, object>> e = r => r.Json.Items[i].Name;
            ((Action)(() => JsonExpressionParser.Parse(e, out _, out _, out _)))
                .Should().Throw<ArgumentException>();
        }
    }
}

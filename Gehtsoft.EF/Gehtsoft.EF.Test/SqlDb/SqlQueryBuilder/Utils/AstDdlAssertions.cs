using AwesomeAssertions;
using Gehtsoft.EF.Test.SqlParser;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    /// <summary>
    /// Semantic, composable assertions for DDL statements. Each expresses one piece of test
    /// intent and hides how the parse tree is navigated. A "HaveDropX" narrows the assertion
    /// subject to that statement node, so follow-ups (e.g. <see cref="HaveIfExists"/>) chain via
    /// <c>.And</c>. Bodies currently walk the tree via the path engine; when the parser backend
    /// changes, only these bodies change — call-sites stay the same.
    /// </summary>
    public static class AstDdlAssertions
    {
        public static AndConstraint<AstNodeAssertions> HaveDropTable(this AstNodeAssertions assertions, string tableName)
        {
            var node = assertions.Subject.SelectNode("/DROP_TABLE[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(tableName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveDropView(this AstNodeAssertions assertions, string viewName)
        {
            var node = assertions.Subject.SelectNode("/DROP_VIEW[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue(viewName);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        public static AndConstraint<AstNodeAssertions> HaveDropIndex(this AstNodeAssertions assertions, string indexFullName, string onTable)
        {
            var node = assertions.Subject.SelectNode("/DROP_INDEX[1]");
            node.Should().Exist();
            node.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue(indexFullName);
            node.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue(onTable);
            return new AndConstraint<AstNodeAssertions>(node.Should());
        }

        /// <summary>Asserts the current statement node carries an IF EXISTS clause.</summary>
        public static AndConstraint<AstNodeAssertions> HaveIfExists(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("/IF_EXIST").Should().Exist();
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
    }
}

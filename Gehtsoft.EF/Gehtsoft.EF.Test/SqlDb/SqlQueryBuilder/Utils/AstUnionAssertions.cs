using System.Collections.Generic;
using AwesomeAssertions;
using Gehtsoft.EF.Test.SqlParser;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    /// <summary>
    /// Semantic navigation + assertions for UNION statements. Navigation returns nodes for
    /// follow-up checks with the shared SELECT vocabulary; assertions express intent. Bodies
    /// walk the tree via the path engine for now (swapped to ANTLR in Phase B).
    /// </summary>
    public static class AstUnionAssertions
    {
        public static IAstNode Union(this IAstNode ast) => ast.SelectNode("/UNION");
        public static IEnumerable<IAstNode> UnionSelects(this IAstNode union) => union.Select("/SELECT");
        public static IAstNode UnionSelect(this IAstNode union, int index) => union.SelectStatement(index);

        public static AndConstraint<AstNodeAssertions> HaveUnionAll(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("/UNION_OP/*").Should().HaveSymbol("UNION_ALL");
            return new AndConstraint<AstNodeAssertions>(assertions);
        }

        public static AndConstraint<AstNodeAssertions> HaveUnionDistinct(this AstNodeAssertions assertions)
        {
            assertions.Subject.SelectNode("/UNION_OP/*").Should().HaveSymbol("UNION_DISTINCT");
            return new AndConstraint<AstNodeAssertions>(assertions);
        }
    }
}

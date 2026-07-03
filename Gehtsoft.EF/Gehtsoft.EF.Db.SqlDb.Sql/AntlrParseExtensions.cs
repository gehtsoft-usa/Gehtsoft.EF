using Antlr4.Runtime;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    /// <summary>
    /// Helpers bridging ANTLR parse-tree nodes to the line/column and text conventions
    /// the Code DOM walkers expect. Hime reported 1-based columns; ANTLR's
    /// <see cref="IToken.Column"/> is 0-based, so <see cref="Col"/> adds 1.
    /// </summary>
    internal static class AntlrParseExtensions
    {
        internal static int Line(this ParserRuleContext ctx) => ctx?.Start?.Line ?? 0;
        internal static int Col(this ParserRuleContext ctx) => (ctx?.Start?.Column ?? -1) + 1;
    }
}

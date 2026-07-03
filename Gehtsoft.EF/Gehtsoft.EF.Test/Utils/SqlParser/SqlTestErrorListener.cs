using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace Gehtsoft.EF.Test.SqlParser
{
    /// <summary>
    /// Collects ANTLR lexer and parser syntax errors so <see cref="SqlParserExtensions.ParseSql"/>
    /// can throw the same diagnostic it did under Hime. Columns are reported 1-based to match the
    /// old convention.
    /// </summary>
    internal sealed class SqlTestErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        internal List<string> Errors { get; } = new List<string>();

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => Errors.Add($"[{line}:{charPositionInLine + 1} - {msg}]");

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => Errors.Add($"[{line}:{charPositionInLine + 1} - {msg}]");
    }
}

using Antlr4.Runtime;
using System.IO;

namespace Gehtsoft.EF.Db.SqlDb.Sql
{
    /// <summary>
    /// ANTLR syntax-error listener that collects lexer and parser errors into a
    /// <see cref="SqlErrorCollection"/>. Replaces Hime's <c>ParseResult.Errors</c>.
    /// Columns are converted from ANTLR's 0-based value to the 1-based convention Hime used.
    /// </summary>
    internal sealed class SqlErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        private readonly string mSource;
        internal SqlErrorCollection Errors { get; } = new SqlErrorCollection();
        internal bool HasErrors => Errors.Count > 0;

        internal SqlErrorListener(string source)
        {
            mSource = source;
        }

        // parser errors (IAntlrErrorListener<IToken> via BaseErrorListener)
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add(mSource, line, charPositionInLine + 1, msg);
        }

        // lexer errors (IAntlrErrorListener<int>)
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add(mSource, line, charPositionInLine + 1, msg);
        }
    }
}

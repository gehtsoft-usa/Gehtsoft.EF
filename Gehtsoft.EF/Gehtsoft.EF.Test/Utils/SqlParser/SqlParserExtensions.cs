using System;
using System.Text;
using Antlr4.Runtime;

namespace Gehtsoft.EF.Test.SqlParser
{
    public static class SqlParserExtensions
    {
        public static IAstNode ParseSql(this string source)
        {
            var lexer = new SqlTestLexer(new AntlrInputStream(source));
            var errors = new SqlTestErrorListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errors);

            var parser = new SqlTestParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errors);

            var root = parser.root();

            if (errors.Errors.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var e in errors.Errors)
                    sb.Append(e);
                throw new ArgumentException($"Parsing of the SQL code failed {sb}", nameof(source));
            }

            return new SqlTestAstBuilder().Visit(root);
        }
    }
}

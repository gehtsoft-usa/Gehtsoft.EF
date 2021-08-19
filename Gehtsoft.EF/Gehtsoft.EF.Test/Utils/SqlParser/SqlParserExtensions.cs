using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions.Collections;
using FluentAssertions.Primitives;
using Hime.Redist;
using MongoDB.Driver.Core.Events;

namespace Gehtsoft.EF.Test.SqlParser
{
    public static class SqlParserExtensions
    {
        public static IAstNode ParseSql(this string source)
        {
            var parser = new SqlTestParser(new SqlTestLexer(source));
            var r = parser.Parse();
            if (!r.IsSuccess)
            {
                var sb = new StringBuilder();
                foreach (ParseError e in r.Errors)
                {
                    sb.Append('[')
                        .Append(e.Position.Line)
                        .Append(':')
                        .Append(e.Position.Column)
                        .Append(" - ")
                        .Append(e.Message)
                        .Append(']');
                }
                throw new ArgumentException($"Parsing of the SQL code failed {sb}", nameof(source));
            }
            return new AstNodeWrapper(r.Root);
        }
    }
}

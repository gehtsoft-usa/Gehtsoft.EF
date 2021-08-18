using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions.Collections;
using FluentAssertions.Primitives;
using Gehtsoft.Ef.Test.SqlParser;
using Hime.Redist;
using MongoDB.Driver.Core.Events;

namespace Gehtsoft.EF.Test.SqlParser
{
    public static class SqlParserExtensions
    {
        private static void Add(XmlDocument document, ASTNode node, XmlElement parent = null, string source = null)
        {
            XmlElement element = document.CreateElement("node");
            var a = document.CreateAttribute("symbol");
            a.Value = node.Symbol.Name;
            element.Attributes.Append(a);
            a = document.CreateAttribute("value");
            a.Value = node.Value ?? "";
            element.Attributes.Append(a);

            element.AppendChild(document.CreateTextNode(node.Context.Content));

            if (parent == null)
                document.AppendChild(element);
            else
                parent.AppendChild(element);

            if (node.Children.Count > 0)
                AddChildren(document, node.Children, element, source);
        }

        private static void AddChildren(XmlDocument document, ASTFamily family, XmlElement parent, string source)
        {
            for (int i = 0; i < family.Count; i++)
                Add(document, family[i], parent, source);
        }

        public static XmlDocument ToXml(this ASTNode node, string source = null)
        {
            var document = new XmlDocument();
            Add(document, node, null, source);
            return document;
        }

        public static XmlDocument ToAstXml(this string source)
        {
            var parser = new Gehtsoft.Ef.Test.SqlParser.SqlParser(new SqlLexer(source));
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
            return r.Root.ToXml(source);
        }
    }
}

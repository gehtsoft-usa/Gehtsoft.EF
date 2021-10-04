using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Xml;
using System.Xml.Schema;

namespace Gehtsoft.EF.Db.SqlDb.OData
{
    internal class DocumentBuilder
    {
        private readonly Stack<XmlNode> mCurrentTarget = new Stack<XmlNode>();
        private readonly Dictionary<string, string> mNamespaces = new Dictionary<string, string>();
        private readonly XmlSchema mSchema;

        public XmlDocument Document { get; }

        public DocumentBuilder()
        {
            Document = new XmlDocument();
            mCurrentTarget.Push(Document);
            mSchema = new XmlSchema();
        }

        public void AddNamespace(string prefix, string ns)
        {
            if (Document.DocumentElement != null)
                throw new InvalidOperationException("Can't add namespaces when the root element is already created");

            mNamespaces.Add(prefix, ns);
            mSchema.Namespaces.Add(prefix, ns);
        }

        private string PrefixToNs(string prefix)
        {
            if (prefix.StartsWith("http://") || prefix.StartsWith("https://"))
                return prefix;

            string ns = null;
            if (mNamespaces.Count > 0 && !mNamespaces.TryGetValue(prefix, out ns))
                throw new ArgumentException($"There is no namespace prefix {prefix} defined", nameof(prefix));

            return ns;
        }

        public DocumentBuilder AppendText(string text)
        {
            var node = Document.CreateTextNode(text);
            mCurrentTarget.Peek().AppendChild(node);
            return this;
        }

        public DocumentBuilder AppendAttribute(string name, string value) => AppendAttribute(name, "", value);

        public DocumentBuilder AppendAttribute(string name, string prefix, string value)
        {
            XmlAttribute attr;
            string ns = "";

            if (!string.IsNullOrEmpty(prefix))
                ns = PrefixToNs(prefix);

            if (string.IsNullOrEmpty(ns))
                attr = Document.CreateAttribute(name);
            else if (ns != prefix)
                attr = Document.CreateAttribute(prefix, name, ns);
            else
                attr = Document.CreateAttribute(name, ns);

            attr.Value = value;
            mCurrentTarget.Peek().Attributes.Append(attr);
            return this;
        }

        public DocumentBuilder Done()
        {
            mCurrentTarget.Pop();
            return this;
        }

        public DocumentBuilder AppendElement(string name) => AppendElement(name, "");

        public DocumentBuilder AppendElement(string name, string prefix)
        {
            bool addSchemaAttributes = false;
            if (ReferenceEquals(mCurrentTarget.Peek(), Document))
            {
                if (Document.DocumentElement != null)
                    throw new InvalidOperationException("Only one top-level element is allowed");
                Document.Schemas.Add(mSchema);
                addSchemaAttributes = true;
            }

            XmlElement el;

            var ns = PrefixToNs(prefix);

            if (string.IsNullOrEmpty(ns))
                el = Document.CreateElement(name);
            else if (ns != prefix)
                el = Document.CreateElement(prefix, name, ns);
            else
                el = Document.CreateElement(name, ns);

            if (addSchemaAttributes)
            {
                foreach (var pair in mNamespaces)
                {
                    if (pair.Key != "")
                    {
                        var attr = Document.CreateAttribute($"xmlns:{pair.Key}");
                        attr.Value = pair.Value;
                        el.Attributes.Append(attr);
                    }
                    else
                    {
                        var attr = Document.CreateAttribute($"xmlns");
                        attr.Value = pair.Value;
                        el.Attributes.Append(attr);
                    }
                }
            }

            mCurrentTarget.Peek().AppendChild(el);
            mCurrentTarget.Push(el);
            return this;
        }

        public DocumentBuilder AtomElement(string prefix, string element, string id, string now, string title = null, string author = null)
        {
            AppendElement(element, prefix)
               .AppendElement("id", prefix)
                   .AppendText(id)
                   .Done()
               .AppendElement("title", prefix)
                   .AppendAttribute("type", "text")
                   .AppendText(title)
                   .Done()
               .AppendElement("updated", prefix)
                   .AppendText(now)
                   .Done()
               .AppendElement("author", prefix)
                       .AppendElement("name", prefix)
                       .AppendText(author)
                       .Done()
                   .Done();

            return this;
        }
    }
}
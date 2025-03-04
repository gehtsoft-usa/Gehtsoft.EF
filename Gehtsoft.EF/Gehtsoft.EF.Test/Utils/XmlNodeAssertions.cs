using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using FluentAssertions.Xml;

namespace Gehtsoft.EF.Test.Utils
{
    public static class XmlNodeAssertionsExtension
    {
        private static XmlNamespaceManager GetNsManager(XmlNode node)
        {
            XmlDocument document;

            if (node is XmlDocument d)
                document = d;
            else
                document = node.OwnerDocument;

            if (document == null)
                throw new InvalidOperationException("The specified node is not a part of a document");

            var root = document.DocumentElement;

            XmlNamespaceManager nsm = new XmlNamespaceManager(document.NameTable);
            bool nullPrefixAdded = false;
            for (int i = 0; i < root.Attributes.Count; i++)
            {
                if (root.Attributes[i].Name.StartsWith("xmlns:"))
                {
                    var prefix = root.Attributes[i].Name.Substring(6);
                    nsm.AddNamespace(prefix, root.Attributes[i].Value);
                }
                else if (root.Attributes[i].Name == "xmlns")
                {
                    nullPrefixAdded = true;
                    nsm.AddNamespace("", root.Attributes[i].Value);
                }
            }
            if (!nullPrefixAdded && !string.IsNullOrEmpty(document.NamespaceURI))
                nsm.AddNamespace("", document.NamespaceURI);
            return nsm;
        }

        public static XmlNode SelectSingleNodeEx(this XmlNode root, string xpath, int index = 0)
        {
            var nodes = root.SelectNodes(xpath, GetNsManager(root));
            if (nodes == null || nodes.Count <= index)
                return null;
            return nodes[index];
        }

        public static AndWhichConstraint<XmlNodeAssertions, XmlNode> HaveElement(this XmlNodeAssertions assertions, string path, string because = null, params object[] becauseArgs)
        {
            XmlNode which = null;
            var nodes = assertions.Subject.SelectNodes(path, GetNsManager(assertions.Subject));
            if (nodes == null || nodes.Count == 0)
                which = null;
            else
                which = nodes[0];

            assertions.CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .Given(() => nodes)
                .ForCondition(list => list != null && list.Count > 0)
                .FailWith("Expected the document has elements matching XPath {0} but it does not", path);
            return new AndWhichConstraint<XmlNodeAssertions, XmlNode>(assertions, which);
        }

        public static AndWhichConstraint<XmlNodeAssertions, XmlNode> HaveElementWithValue(this XmlNodeAssertions assertions, string path, string value, string because = null, params object[] becauseArgs)
        {
            XmlNode which = null;
            var nodes = assertions.Subject.SelectNodes(path, GetNsManager(assertions.Subject));
            if (nodes == null || nodes.Count == 0)
                which = null;
            else
                which = nodes[0];

            assertions.CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .Given(() => nodes)
                .ForCondition(list => list != null && list.Count > 0)
                .FailWith("Expected the document has elements matching XPath {0} but it does not", path)
                .Then
                .ForCondition(list => list[0].InnerText == value)
                .FailWith("Expected the document has elements matching XPath {0} with the value {1} but the value is not correct", path, value);
            return new AndWhichConstraint<XmlNodeAssertions, XmlNode>(assertions, which);
        }

        public static AndWhichConstraint<XmlNodeAssertions, XmlNode> HaveElement(this XmlNodeAssertions assertions, string path, int index, string because = null, params object[] becauseArgs)
        {
            XmlNode which = null;
            var nodes = assertions.Subject.SelectNodes(path, GetNsManager(assertions.Subject));
            if (nodes == null || nodes.Count <= index)
                which = null;
            else
                which = nodes[0];

            assertions.CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .Given(() => nodes)
                .ForCondition(list => list != null && list.Count > index)
                .FailWith("Expected the document has at least {1} elements matching XPath {0} but it does not", path, index);
            return new AndWhichConstraint<XmlNodeAssertions, XmlNode>(assertions, which);
        }

        public static AndConstraint<XmlNodeAssertions> HaveAttribute(this XmlNodeAssertions assertions, string attributeName, string attributeValue, string because = null, params object[] becauseArgs)
        {
            assertions.CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .Given(() => assertions.Subject as XmlElement)
                .ForCondition(el => el != null)
                .FailWith("Expected the element be an element but it is not")
                .Then
                .ForCondition(el =>
                {
                    for (int i = 0; i < el.Attributes.Count; i++)
                        if (el.Attributes[i].Name == attributeName)
                            return true;
                    return false;
                })
                .FailWith("Expected the element to have attribute {0} but it does not",attributeName)
                .Then
                .ForCondition(el =>
                {
                    for (int i = 0; i < el.Attributes.Count; i++)
                        if (el.Attributes[i].Name == attributeName)
                        {
                            if (el.Attributes[i].Value == attributeValue)
                                return true;
                            else
                                return false;
                        }
                    return false;
                })
                .FailWith("Expected the element to have attribute {0} have value {1} but it does not", attributeName, attributeValue);

            return new AndConstraint<XmlNodeAssertions>(assertions);
        }

        public static AndConstraint<XmlNodeAssertions> HaveText(this XmlNodeAssertions assertions, string text, string because = null, params object[] becauseArgs)
        {
            assertions.CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .Given(() => assertions.Subject as XmlElement)
                .ForCondition(el => el != null)
                .FailWith("Expected the node be an element but it is not")
                .Then
                .ForCondition(el => el.InnerText == text)
                .FailWith("Expected to have text {0} but it does not", text);

            return new AndConstraint<XmlNodeAssertions>(assertions);
        }

        public static AndConstraint<XmlNodeAssertions> ContainText(this XmlNodeAssertions assertions, string text, string because = null, params object[] becauseArgs)
        {
            assertions.CurrentAssertionChain
                .BecauseOf(because, becauseArgs)
                .Given(() => assertions.Subject as XmlElement)
                .ForCondition(el => el != null)
                .FailWith("Expected the node be an element but it is not")
                .Then
                .ForCondition(el => el.InnerText.Contains(text))
                .FailWith("Expected to contain text {0} but it does not", text);

            return new AndConstraint<XmlNodeAssertions>(assertions);
        }
    }
}

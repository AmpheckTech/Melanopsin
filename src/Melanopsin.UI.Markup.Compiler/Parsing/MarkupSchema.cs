using System;
using System.Linq;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

internal static class MarkupSchema {
    internal const string Namespace = "https://melanopsin.dev/schema";
    internal const string ReferenceAttributeName = "Ref";

    private static readonly string[] MetadataAttributes =
        ["package", "class", "resources"];

    internal static bool IsElement(XElement element, string localName) {
        return element.Name.NamespaceName == Namespace &&
               string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal);
    }

    internal static bool IsAttribute(XAttribute attribute) {
        return attribute.Name.NamespaceName == Namespace;
    }

    internal static bool IsMetadataAttribute(XAttribute attribute) {
        return IsAttribute(attribute) &&
               MetadataAttributes.Contains(
                   attribute.Name.LocalName,
                   StringComparer.Ordinal);
    }

    internal static bool IsReferenceAttribute(MarkupAttribute attribute) {
        return string.Equals(
            attribute.Name,
            ReferenceAttributeName,
            StringComparison.Ordinal);
    }
}

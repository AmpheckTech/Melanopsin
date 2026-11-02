using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing.Templates;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

internal sealed class MarkupTreeParser {
    private readonly MarkupControlTokenizer _controlTokenizer;
    private readonly MarkupInterpolationParser _interpolation;
    private readonly MarkupNodeSequenceParser _sequenceParser;
    private readonly XmlSourceMap _sourceMap;
    private readonly Action<MarkupDiagnostic> _report;

    public MarkupTreeParser(
        XmlSourceMap sourceMap,
        MarkupCodeMask codeMask,
        Action<MarkupDiagnostic> report) {
        _sourceMap = sourceMap;
        _report = report;
        _interpolation = new MarkupInterpolationParser(report);
        _controlTokenizer = new MarkupControlTokenizer(sourceMap, codeMask, report);
        _sequenceParser = new MarkupNodeSequenceParser(
            _interpolation,
            report,
            ParseElement);
    }

    public IReadOnlyList<MarkupCode> CodeBlocks => _sequenceParser.CodeBlocks;

    public MarkupElement ParseRoot(XElement root) {
        return ParseElement(root, isRoot: true);
    }

    private MarkupElement ParseElement(XElement element) {
        return ParseElement(element, isRoot: false);
    }

    private MarkupElement ParseElement(XElement element, bool isRoot) {
        if (element.Name.NamespaceName == MarkupSchema.Namespace) {
            Report(
                MarkupDiagnosticCodes.UnsupportedDirective,
                $"Unsupported view directive <{element.Name.LocalName}>. " +
                "Use Angular-like @if, @else and @for blocks.",
                element,
                element.Name.LocalName.Length);
            return new MarkupElement(
                Offset(element),
                element.Name.NamespaceName,
                GetPrefix(element),
                element.Name.LocalName,
                Array.Empty<MarkupAttribute>(),
                Array.Empty<MarkupNode>());
        }

        var attributes = new List<MarkupAttribute>();
        var attributeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in element.Attributes()) {
            if (attribute.IsNamespaceDeclaration) {
                continue;
            }

            if (MarkupSchema.IsAttribute(attribute)) {
                if (isRoot && MarkupSchema.IsLegacyMetadataAttribute(attribute)) {
                    Report(
                        MarkupDiagnosticCodes.UnsupportedDirective,
                        "The 'm:' namespace is no longer supported. " +
                        "Use plain package, component and resources attributes.",
                        attribute,
                        attribute.Name.LocalName.Length);
                    continue;
                }

                Report(
                    MarkupDiagnosticCodes.UnsupportedDirective,
                    $"Unsupported markup attribute '{attribute.Name.LocalName}' on <{element.Name.LocalName}>.",
                    attribute,
                    attribute.Name.LocalName.Length);
                continue;
            }

            if (attribute.Name.NamespaceName == XNamespace.Xml.NamespaceName) {
                continue;
            }

            var name = attribute.Name.LocalName;
            if (isRoot && MarkupSchema.IsMetadataAttribute(attribute)) {
                continue;
            }

            if (!attributeNames.Add(name)) {
                Report(
                    MarkupDiagnosticCodes.DuplicateAttribute,
                    $"Duplicate attribute '{name}' on <{element.Name.LocalName}>.",
                    attribute,
                    name.Length);
            }

            attributes.Add(new MarkupAttribute(
                Offset(attribute),
                name,
                _interpolation.ParseAttribute(attribute.Value, ValueOffset(attribute))));
        }

        return new MarkupElement(
            Offset(element),
            element.Name.NamespaceName,
            GetPrefix(element),
            element.Name.LocalName,
            attributes,
            ParseNodes(element, isRoot));
    }

    private static string GetPrefix(XElement element) {
        return element.GetPrefixOfNamespace(element.Name.Namespace) ?? string.Empty;
    }

    private IReadOnlyList<MarkupNode> ParseNodes(XElement parent, bool isRoot) {
        var tokens = _controlTokenizer.Tokenize(parent);
        return _sequenceParser.Parse(tokens, allowCode: isRoot);
    }

    private int Offset(XObject source) {
        return _sourceMap.Offset(source);
    }

    /// <summary>
    /// Where an attribute's value starts in the document, or <c>-1</c> when it does not line up with
    /// the source. The reader normalizes entities and line breaks, so a value that no longer matches
    /// the text it came from may only be used as code, never as a location.
    /// </summary>
    private int ValueOffset(XAttribute attribute) {
        var source = _sourceMap.Source;
        var value = attribute.Value;
        var index = _sourceMap.Offset(attribute);
        while (index < source.Length && source[index] != '=') {
            if (source[index] is '"' or '\'' or '<' or '>') {
                return -1;
            }

            index++;
        }

        if (++index >= source.Length) {
            return -1;
        }

        while (index < source.Length && char.IsWhiteSpace(source[index])) {
            index++;
        }

        if (index >= source.Length || source[index] is not ('"' or '\'')) {
            return -1;
        }

        var start = index + 1;
        return start + value.Length <= source.Length &&
               string.CompareOrdinal(source, start, value, 0, value.Length) == 0
            ? start
            : -1;
    }

    private void Report(string code, string message, XObject source, int length) {
        ReportAt(code, message, _sourceMap.Offset(source), length);
    }

    private void ReportAt(string code, string message, int offset, int length) {
        _report(new MarkupDiagnostic(
            code,
            message,
            offset,
            Math.Max(1, length)));
    }
}

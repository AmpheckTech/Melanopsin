// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Parsing;

internal sealed class MarkupParser {
    private readonly List<MarkupDiagnostic> _diagnostics = [];
    private readonly HashSet<string> _namespaceUris = new(StringComparer.Ordinal);
    private readonly XmlSourceMap _sourceMap;
    private readonly MarkupTreeParser _treeParser;
    private string? _namespace;
    private string? _componentName;
    private string? _resourcePackage;

    private MarkupParser(XmlSourceMap sourceMap, MarkupCodeMask codeMask) {
        _sourceMap = sourceMap;
        _treeParser = new MarkupTreeParser(sourceMap, codeMask, ReportDiagnostic);
    }

    public static MarkupDocument Parse(string path, string source) {
        var sourceMap = new XmlSourceMap(source);
        var codeMask = MarkupCodeMask.Apply(source);
        XDocument xml;
        try {
            xml = XDocument.Parse(
                codeMask.Encoded,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        } catch (XmlException exception) {
            return new MarkupDocument(
                path,
                source,
                Array.Empty<MarkupNode>(),
                diagnostics: [sourceMap.CreateDiagnostic(exception)]);
        }

        if (xml.Root is null) {
            return new MarkupDocument(
                path,
                source,
                Array.Empty<MarkupNode>(),
                diagnostics:
                [
                    new MarkupDiagnostic(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "An XML view must contain one root element.",
                        0,
                        1),
                ]);
        }

        var parser = new MarkupParser(sourceMap, codeMask);
        parser.ReadNamespaces(xml.Root);
        var nodes = parser.ParseDocument(xml.Root);

        return new MarkupDocument(
            path,
            source,
            nodes,
            parser._namespace,
            parser._componentName,
            parser._resourcePackage,
            parser._namespaceUris,
            parser._treeParser.CodeBlocks,
            parser._diagnostics);
    }

    private void ReadNamespaces(XElement root) {
        foreach (var attribute in root.DescendantsAndSelf().Attributes()) {
            if (!attribute.IsNamespaceDeclaration || attribute.Value.Length == 0) {
                continue;
            }

            _namespaceUris.Add(attribute.Value);
        }
    }

    private IReadOnlyList<MarkupNode> ParseDocument(XElement root) {
        ReadMetadata(root);

        if (string.IsNullOrWhiteSpace(_namespace)) {
            Report(
                MarkupDiagnosticCodes.InvalidMetadata,
                "The root element must declare package.",
                root,
                1);
        }

        if (!string.IsNullOrWhiteSpace(_componentName) &&
            !SyntaxFacts.IsValidIdentifier(_componentName)) {
            Report(
                MarkupDiagnosticCodes.InvalidMetadata,
                $"'{_componentName}' is not a valid C# component name.",
                root,
                1);
        }

        if (MarkupSchema.IsElement(root, "If") ||
            MarkupSchema.IsElement(root, "For")) {
            Report(
                MarkupDiagnosticCodes.InvalidSyntax,
                "The root element must be a control, not a view directive.",
                root,
                1);
            return Array.Empty<MarkupNode>();
        }

        return [_treeParser.ParseRoot(root)];
    }

    private void ReadMetadata(XElement root) {
        foreach (var attribute in root.Attributes()) {
            if (!MarkupSchema.IsMetadataAttribute(attribute)) {
                continue;
            }

            var value = attribute.Value.Trim();
            if (value.Length == 0) {
                Report(
                    MarkupDiagnosticCodes.InvalidMetadata,
                    $"{attribute.Name.LocalName} requires a value.",
                    attribute,
                    1);
                continue;
            }

            switch (attribute.Name.LocalName) {
                case "package":
                    SetMetadata(ref _namespace, value, "package", attribute);
                    break;
                case "component":
                    SetMetadata(ref _componentName, value, "component", attribute);
                    break;
                case "resources":
                    SetMetadata(
                        ref _resourcePackage,
                        value,
                        "resources",
                        attribute);
                    break;
            }
        }
    }

    private void SetMetadata(
        ref string? target,
        string value,
        string name,
        XObject source) {
        if (!string.IsNullOrEmpty(target)) {
            Report(
                MarkupDiagnosticCodes.InvalidMetadata,
                $"{name} may only be declared once.",
                source,
                name.Length);
            return;
        }

        target = value;
    }

    private void Report(string code, string message, XObject source, int length) {
        ReportAt(code, message, _sourceMap.Offset(source), length);
    }

    private void ReportAt(string code, string message, int offset, int length) {
        ReportDiagnostic(new MarkupDiagnostic(
            code,
            message,
            offset,
            Math.Max(1, length)));
    }

    private void ReportDiagnostic(MarkupDiagnostic diagnostic) {
        _diagnostics.Add(diagnostic);
    }
}

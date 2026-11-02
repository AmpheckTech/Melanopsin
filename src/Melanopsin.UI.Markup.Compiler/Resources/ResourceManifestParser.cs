using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Parsing;
using Microsoft.CodeAnalysis.CSharp;

namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed partial class ResourceManifestParser {
    private const string ResourceNamespace = "https://melanopsin.dev/resources";
    private readonly List<MarkupDiagnostic> _diagnostics = [];
    private readonly XmlSourceMap _sourceMap;
    private string? _package;
    private string? _className;

    private ResourceManifestParser(XmlSourceMap sourceMap) {
        _sourceMap = sourceMap;
    }

    public static ResourceManifest Parse(string path, string source) {
        var sourceMap = new XmlSourceMap(source);
        XDocument document;
        try {
            document = XDocument.Parse(
                source,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        } catch (XmlException exception) {
            return new ResourceManifest(
                path,
                source,
                string.Empty,
                "R",
                Array.Empty<ResourceDefinition>(),
                [sourceMap.CreateDiagnostic(exception)]);
        }

        if (document.Root is null) {
            return new ResourceManifest(
                path,
                source,
                string.Empty,
                "R",
                Array.Empty<ResourceDefinition>(),
                [
                    new MarkupDiagnostic(
                        MarkupDiagnosticCodes.InvalidResourceManifest,
                        "A resource manifest must contain one root element.",
                        0,
                        1),
                ]);
        }

        var parser = new ResourceManifestParser(sourceMap);
        var definitions = parser.ParseRoot(document.Root);
        return new ResourceManifest(
            path,
            source,
            parser._package ?? string.Empty,
            parser._className ?? "R",
            definitions,
            parser._diagnostics);
    }

    private void ReadMetadata(XElement root) {
        foreach (var attribute in root.Attributes()) {
            if (attribute.Name.NamespaceName != ResourceNamespace) {
                continue;
            }

            var value = attribute.Value.Trim();
            switch (attribute.Name.LocalName) {
                case "package":
                    if (value.Length == 0) {
                        Report(
                            MarkupDiagnosticCodes.InvalidResourceManifest,
                            "m:package requires a value.",
                            attribute,
                            1);
                    } else if (SyntaxFactory.ParseName(value).ContainsDiagnostics) {
                        Report(
                            MarkupDiagnosticCodes.InvalidResourceManifest,
                            $"'{value}' is not a valid C# namespace.",
                            attribute,
                            1);
                    } else {
                        _package = value;
                    }

                    break;

                case "class":
                    if (!SyntaxFacts.IsValidIdentifier(value)) {
                        Report(
                            MarkupDiagnosticCodes.InvalidResourceManifest,
                            $"'{value}' is not a valid C# class name.",
                            attribute,
                            1);
                    } else {
                        _className = value;
                    }

                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(_package)) {
            Report(
                MarkupDiagnosticCodes.InvalidResourceManifest,
                "The root element must declare m:package.",
                root,
                1);
        }
    }

    private void Report(
        string code,
        string message,
        XObject source,
        int length) {
        _diagnostics.Add(new MarkupDiagnostic(
            code,
            message,
            _sourceMap.Offset(source),
            Math.Max(1, length)));
    }
}

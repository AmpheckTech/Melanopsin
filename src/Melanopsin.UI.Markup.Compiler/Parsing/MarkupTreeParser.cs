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
    private readonly MarkupLoopHeaderParser _loopHeaderParser;
    private readonly XmlSourceMap _sourceMap;
    private readonly Action<MarkupDiagnostic> _report;

    public MarkupTreeParser(
        XmlSourceMap sourceMap,
        Action<MarkupDiagnostic> report) {
        _sourceMap = sourceMap;
        _report = report;
        _interpolation = new MarkupInterpolationParser(report);
        _controlTokenizer = new MarkupControlTokenizer(sourceMap, report);
        _loopHeaderParser = new MarkupLoopHeaderParser(report);
    }

    public MarkupElement ParseRoot(XElement root) {
        return ParseElement(root, isRoot: true);
    }

    private MarkupElement ParseElement(XElement element, bool isRoot = false) {
        if (element.Name.NamespaceName == MarkupSchema.Namespace) {
            Report(
                MarkupDiagnosticCodes.UnsupportedDirective,
                $"Unsupported view directive <{element.Name.LocalName}>. " +
                "Use Angular-like @if, @else and @for blocks.",
                element,
                element.Name.LocalName.Length);
            return new MarkupElement(
                Offset(element),
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
                if (isRoot && MarkupSchema.IsMetadataAttribute(attribute)) {
                    continue;
                }

                if (isRoot && attribute.Name.LocalName == "model") {
                    Report(
                        MarkupDiagnosticCodes.UnsupportedDirective,
                        "m:model is no longer supported. Bind directly to members of the component class.",
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
                _interpolation.ParseAttribute(attribute.Value, Offset(attribute))));
        }

        return new MarkupElement(
            Offset(element),
            element.Name.LocalName,
            attributes,
            ParseNodes(element));
    }

    private IReadOnlyList<MarkupNode> ParseNodes(XElement parent) {
        var tokens = _controlTokenizer.Tokenize(parent);
        var index = 0;
        return ParseSequence(
            tokens,
            ref index,
            stopAtEndBlock: false,
            out _);
    }

    private IReadOnlyList<MarkupNode> ParseSequence(
        IReadOnlyList<MarkupTemplateToken> tokens,
        ref int index,
        bool stopAtEndBlock,
        out bool ended) {
        var nodes = new List<MarkupNode>();
        ended = false;
        while (index < tokens.Count) {
            var token = tokens[index];
            switch (token.Kind) {
                case MarkupTemplateTokenKind.Text:
                    _interpolation.ParseText(nodes, token.Value, token.Offset);
                    index++;
                    break;

                case MarkupTemplateTokenKind.Element:
                    nodes.Add(ParseElement(token.Element!));
                    index++;
                    break;

                case MarkupTemplateTokenKind.If:
                    index++;
                    nodes.Add(ParseConditional(token, tokens, ref index));
                    break;

                case MarkupTemplateTokenKind.For:
                    index++;
                    nodes.Add(ParseLoop(token, tokens, ref index));
                    break;

                case MarkupTemplateTokenKind.Switch:
                    index++;
                    nodes.Add(ParseSwitch(token, tokens, ref index));
                    break;

                case MarkupTemplateTokenKind.EndBlock:
                    index++;
                    if (stopAtEndBlock) {
                        ended = true;
                        return nodes;
                    }

                    ReportAt(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "Unexpected '}'.",
                        token.Offset,
                        1);
                    break;

                case MarkupTemplateTokenKind.Else:
                case MarkupTemplateTokenKind.ElseIf:
                    index++;
                    ReportAt(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "@else must immediately follow an @if block.",
                        token.Offset,
                        1);
                    break;

                case MarkupTemplateTokenKind.Empty:
                    index++;
                    ReportAt(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "@empty must immediately follow an @for block.",
                        token.Offset,
                        1);
                    break;

                case MarkupTemplateTokenKind.Case:
                    index++;
                    ReportAt(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "@case must be inside an @switch block.",
                        token.Offset,
                        1);
                    break;

                case MarkupTemplateTokenKind.Default:
                    index++;
                    ReportAt(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "@default must be inside an @switch block.",
                        token.Offset,
                        1);
                    break;
            }
        }

        return nodes;
    }

    private MarkupIf ParseConditional(
        MarkupTemplateToken token,
        IReadOnlyList<MarkupTemplateToken> tokens,
        ref int index) {
        var whenTrue = ParseBlock(tokens, ref index, token.Offset, "if");
        var whenFalse = new List<MarkupNode>();

        if (index < tokens.Count && tokens[index].Kind == MarkupTemplateTokenKind.ElseIf) {
            var elseIf = tokens[index++];
            whenFalse.Add(ParseConditional(elseIf, tokens, ref index));
        } else if (index < tokens.Count && tokens[index].Kind == MarkupTemplateTokenKind.Else) {
            index++;
            whenFalse.AddRange(ParseBlock(tokens, ref index, token.Offset, "else"));
        }

        return new MarkupIf(token.Offset, token.Value, whenTrue, whenFalse);
    }

    private MarkupFor ParseLoop(
        MarkupTemplateToken token,
        IReadOnlyList<MarkupTemplateToken> tokens,
        ref int index) {
        var header = _loopHeaderParser.Parse(token.Value, token.Offset);
        var body = ParseBlock(tokens, ref index, token.Offset, "for");
        var emptyBody = new List<MarkupNode>();
        if (index < tokens.Count &&
            tokens[index].Kind == MarkupTemplateTokenKind.Empty) {
            var empty = tokens[index++];
            emptyBody.AddRange(ParseBlock(tokens, ref index, empty.Offset, "empty"));
        }

        return new MarkupFor(
            token.Offset,
            header.Variable,
            header.Iterable,
            header.Track,
            body,
            emptyBody);
    }

    private MarkupSwitch ParseSwitch(
        MarkupTemplateToken token,
        IReadOnlyList<MarkupTemplateToken> tokens,
        ref int index) {
        var cases = new List<MarkupSwitchCase>();
        IReadOnlyList<MarkupNode> defaultBody = [];
        var hasDefault = false;
        var ended = false;
        while (index < tokens.Count) {
            var bodyToken = tokens[index];
            switch (bodyToken.Kind) {
                case MarkupTemplateTokenKind.Case:
                    index++;
                    cases.Add(new MarkupSwitchCase(
                        bodyToken.Offset,
                        bodyToken.Value,
                        ParseBlock(tokens, ref index, bodyToken.Offset, "case")));
                    break;

                case MarkupTemplateTokenKind.Default:
                    index++;
                    if (hasDefault) {
                        ReportAt(
                            MarkupDiagnosticCodes.InvalidSyntax,
                            "An @switch block may contain only one @default block.",
                            bodyToken.Offset,
                            1);
                    }

                    hasDefault = true;
                    defaultBody = ParseBlock(
                        tokens,
                        ref index,
                        bodyToken.Offset,
                        "default");
                    break;

                case MarkupTemplateTokenKind.EndBlock:
                    index++;
                    ended = true;
                    break;

                default:
                    index++;
                    ReportAt(
                        MarkupDiagnosticCodes.InvalidSyntax,
                        "@switch bodies may only contain @case and @default blocks.",
                        bodyToken.Offset,
                        1);
                    break;
            }

            if (ended) {
                break;
            }
        }

        if (!ended) {
            ReportAt(
                MarkupDiagnosticCodes.InvalidSyntax,
                "Unterminated @switch block.",
                token.Offset,
                1);
        }

        if (cases.Count == 0 && !hasDefault) {
            ReportAt(
                MarkupDiagnosticCodes.InvalidSyntax,
                "An @switch block requires at least one @case or @default block.",
                token.Offset,
                1);
        }

        return new MarkupSwitch(token.Offset, token.Value, cases, defaultBody);
    }

    private IReadOnlyList<MarkupNode> ParseBlock(
        IReadOnlyList<MarkupTemplateToken> tokens,
        ref int index,
        int directiveOffset,
        string directive) {
        var body = ParseSequence(
            tokens,
            ref index,
            stopAtEndBlock: true,
            out var ended);
        if (!ended) {
            ReportAt(
                MarkupDiagnosticCodes.InvalidSyntax,
                $"Unterminated @{directive} block.",
                directiveOffset,
                1);
        }

        return body;
    }

    private int Offset(XObject source) {
        return _sourceMap.Offset(source);
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

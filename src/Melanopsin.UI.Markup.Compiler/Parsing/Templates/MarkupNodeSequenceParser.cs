using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Melanopsin.UI.Markup.Compiler.Diagnostics;
using Melanopsin.UI.Markup.Compiler.Syntax;

namespace Melanopsin.UI.Markup.Compiler.Parsing.Templates;

internal sealed class MarkupNodeSequenceParser {
    private readonly MarkupInterpolationParser _interpolation;
    private readonly MarkupLoopHeaderParser _loopHeaderParser;
    private readonly Func<XElement, MarkupElement> _parseElement;
    private readonly Action<MarkupDiagnostic> _report;
    private readonly List<MarkupCode> _codeBlocks = [];

    public MarkupNodeSequenceParser(
        MarkupInterpolationParser interpolation,
        Action<MarkupDiagnostic> report,
        Func<XElement, MarkupElement> parseElement) {
        _interpolation = interpolation;
        _report = report;
        _parseElement = parseElement;
        _loopHeaderParser = new MarkupLoopHeaderParser(report);
    }

    public IReadOnlyList<MarkupCode> CodeBlocks => _codeBlocks;

    public IReadOnlyList<MarkupNode> Parse(
        IReadOnlyList<MarkupTemplateToken> tokens,
        bool allowCode) {
        tokens = ExtractCodeBlocks(tokens, allowCode);
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
                    nodes.Add(_parseElement(token.Element!));
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

    /// <summary>
    /// Removes <c>@code</c> tokens from the element stream. A code block belongs to the component
    /// class rather than the element tree, so it is only accepted directly inside the root element.
    /// </summary>
    private IReadOnlyList<MarkupTemplateToken> ExtractCodeBlocks(
        IReadOnlyList<MarkupTemplateToken> tokens,
        bool allowCode) {
        var filtered = new List<MarkupTemplateToken>(tokens.Count);
        foreach (var token in tokens) {
            if (token.Kind != MarkupTemplateTokenKind.Code) {
                filtered.Add(token);
                continue;
            }

            if (allowCode) {
                _codeBlocks.Add(new MarkupCode(token.Offset, token.Value));
                continue;
            }

            ReportAt(
                MarkupDiagnosticCodes.InvalidSyntax,
                "@code must be a direct child of the root element.",
                token.Offset,
                "@code".Length);
        }

        return filtered;
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

        return new MarkupIf(token.Offset, token.ValueOffset, token.Value, whenTrue, whenFalse);
    }

    private MarkupFor ParseLoop(
        MarkupTemplateToken token,
        IReadOnlyList<MarkupTemplateToken> tokens,
        ref int index) {
        // The header parser locates the iterable and the track clause from the header's own start,
        // which the tokenizer records as the token's value offset.
        var headerOffset = token.ValueOffset >= 0 ? token.ValueOffset : token.Offset;
        var header = _loopHeaderParser.Parse(token.Value, headerOffset);
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
            header.IterableOffset,
            header.Iterable,
            header.TrackOffset,
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
                        bodyToken.ValueOffset,
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

        return new MarkupSwitch(
            token.Offset,
            token.ValueOffset,
            token.Value,
            cases,
            defaultBody);
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

    private void ReportAt(string code, string message, int offset, int length) {
        _report(new MarkupDiagnostic(
            code,
            message,
            offset,
            Math.Max(1, length)));
    }
}

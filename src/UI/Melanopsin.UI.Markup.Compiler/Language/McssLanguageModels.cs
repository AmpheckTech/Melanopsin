// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>A stylesheet supplied to MCSS analysis, independent of its project.</summary>
public sealed record McssSourceFile(string Path, string Text);

/// <summary>A diagnostic produced by the same parser and validators used by compilation.</summary>
public sealed record McssDiagnosticInfo(
    string Code,
    string Message,
    int Offset,
    int Length);

/// <summary>The kind of an MCSS completion item.</summary>
public enum McssCompletionKind {
    Directive,
    Property,
    Token,
    Value,
    Selector,
    Snippet,
}

/// <summary>An editor completion derived from MCSS syntax and the active project's stylesheets.</summary>
public sealed record McssCompletionItem(
    string Label,
    string Detail,
    string InsertText,
    McssCompletionKind Kind,
    string? Documentation = null);

/// <summary>Hover content and the source span that owns it.</summary>
public sealed record McssHoverInfo(
    string Markdown,
    int Offset,
    int Length);

/// <summary>A semantic token category used by editor clients.</summary>
public enum McssSemanticTokenKind {
    Directive,
    Type,
    Class,
    Property,
    State,
    Variable,
    Value,
    Number,
    Comment,
}

/// <summary>A semantic token over an original stylesheet span.</summary>
public sealed record McssSemanticToken(
    int Offset,
    int Length,
    McssSemanticTokenKind Kind);

/// <summary>A source span, identified by the path of the document that owns it.</summary>
public sealed record McssSourceSpan(
    string Path,
    int Offset,
    int Length);

/// <summary>A named value declared in an <c>@tokens</c> block.</summary>
public sealed record McssTokenInfo(
    string Name,
    string TypeName,
    string Value,
    int NameOffset,
    int NameLength,
    int ValueOffset,
    int ValueLength);

/// <summary>An import path and, when present in the workspace, the file it resolves to.</summary>
public sealed record McssImportInfo(
    string Path,
    int Offset,
    int Length,
    string? TargetPath);

/// <summary>A compiled style rule presented as an editor symbol.</summary>
public sealed record McssRuleInfo(
    string Selector,
    int Offset,
    int Length);

/// <summary>Syntax, symbols and diagnostics for one MCSS document.</summary>
public sealed record McssAnalysis(
    string Path,
    IReadOnlyList<McssDiagnosticInfo> Diagnostics,
    IReadOnlyList<McssTokenInfo> Tokens,
    IReadOnlyList<McssImportInfo> Imports,
    IReadOnlyList<McssRuleInfo> Rules);

// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>Describes one opening and optional closing tag in an MXML document.</summary>
public sealed record MarkupTagPair(
    string Name,
    int OpeningTagStart,
    int OpeningTagEnd,
    int OpeningNameStart,
    int OpeningNameLength,
    int? ClosingTagStart,
    int? ClosingTagEnd,
    int? ClosingNameStart,
    int? ClosingNameLength,
    bool IsSelfClosing,
    bool HasContent,
    bool IsRoot)
{
    public bool IsPaired => ClosingTagStart is not null;
}

/// <summary>The kind of recovery issue found while pairing raw MXML tags.</summary>
public enum MarkupTagIssueKind {
    UnexpectedClosingTag,
    UnclosedOpeningTag,
}

/// <summary>A tag-pairing issue that can be repaired by an editor action.</summary>
public sealed record MarkupTagIssue(
    MarkupTagIssueKind Kind,
    int TagStart,
    int TagEnd,
    int NameStart,
    int NameLength,
    string Name,
    string? ExpectedName);

/// <summary>A text edit produced by an editor-facing markup feature.</summary>
public sealed record MarkupTextEdit(int Offset, int Length, string NewText);

/// <summary>A source span used by editor selection and navigation features.</summary>
public sealed record MarkupSourceSpan(int Offset, int Length);

/// <summary>A quick fix or refactoring offered for a tag span.</summary>
public sealed record MarkupCodeActionInfo(
    string Title,
    string Kind,
    IReadOnlyList<MarkupTextEdit> Edits,
    bool IsPreferred = false);

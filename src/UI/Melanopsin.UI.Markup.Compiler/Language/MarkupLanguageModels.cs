// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace Melanopsin.UI.Markup.Compiler.Language;

/// <summary>Describes a C# declaration that a markup symbol resolves to.</summary>
public sealed record MarkupDefinitionTarget(
    string TypeName,
    string? MemberName = null,
    string? AttributeName = null);

/// <summary>A diagnostic produced while analyzing a markup document.</summary>
public sealed record MarkupDiagnosticInfo(
    string Code,
    string Message,
    int Offset,
    int Length);

/// <summary>A markup attribute and its control schema.</summary>
public sealed record MarkupAttributeInfo(
    string Name,
    int Offset,
    int Length,
    string? TypeName,
    string ValueKind,
    IReadOnlyList<string> AllowedValues,
    MarkupDefinitionTarget? Definition);

/// <summary>A markup event and its delegate schema.</summary>
public sealed record MarkupEventInfo(
    string Name,
    string DelegateTypeName,
    int ParameterCount,
    string? ArgumentTypeName);

/// <summary>A control resolved from a markup element.</summary>
public sealed record MarkupControlInfo(
    string Name,
    string Namespace,
    string AssemblyName,
    string TypeName,
    bool IsComponent,
    IReadOnlyList<MarkupAttributeInfo> Attributes,
    IReadOnlyList<MarkupEventInfo> Events)
{
    public string QualifiedName => Namespace.Length == 0
        ? Name
        : Namespace + "." + Name;

    public MarkupDefinitionTarget Definition => new(TypeName);
}

/// <summary>A markup element at its original source span.</summary>
public sealed record MarkupElementInfo(
    string Name,
    string QualifiedName,
    string NamespaceUri,
    int Offset,
    int NameLength,
    MarkupControlInfo? Control,
    IReadOnlyList<MarkupAttributeInfo> Attributes,
    IReadOnlyList<MarkupElementInfo> Children);

/// <summary>A C# body declared by an <c>@code</c> block, at its original source span.</summary>
public sealed record MarkupCodeBlockInfo(int Offset, int Length);

/// <summary>A markup document supplied to the analysis, independent of the project it belongs to.</summary>
public sealed record MarkupSourceFile(string Path, string Text);

/// <summary>The schema and syntax information for one markup document.</summary>
public sealed record MarkupAnalysis(
    string Path,
    string? Namespace,
    string? ComponentName,
    MarkupDefinitionTarget? Component,
    IReadOnlyList<MarkupDiagnosticInfo> Diagnostics,
    IReadOnlyList<MarkupElementInfo> Elements,
    IReadOnlyList<MarkupCodeBlockInfo> CodeBlocks);

/// <summary>The kind of a markup completion item.</summary>
public enum MarkupCompletionKind {
    Class,
    Property,
    Field,
    Method,
    Event,
    Keyword,
    Value,
    Snippet,
}

/// <summary>A completion candidate derived from the markup schema or component type.</summary>
public sealed record MarkupCompletionItem(
    string Label,
    string Detail,
    string InsertText,
    MarkupCompletionKind Kind,
    bool IsSnippet = false,
    string? Documentation = null,
    MarkupDefinitionTarget? Definition = null);

/// <summary>Hover content and the source span that owns it.</summary>
public sealed record MarkupHoverInfo(
    string Markdown,
    int Offset,
    int Length,
    MarkupDefinitionTarget? Definition = null);

/// <summary>A semantic token category used by editor clients.</summary>
public enum MarkupSemanticTokenKind {
    /// <summary>A markup element or component tag name, reported to clients as <c>type</c>.</summary>
    Element,
    /// <summary>A user-defined component tag name, reported to clients as <c>class</c>.</summary>
    Component,
    /// <summary>An MCSS class name referenced by a <c>class</c> attribute.</summary>
    ClassName,
    Property,
    Event,
    Keyword,
    Variable,
    /// <summary>A C# method referenced from an embedded expression or event handler.</summary>
    Function,
    /// <summary>An attribute value that matches a declared enum or allowed value.</summary>
    EnumValue,
    /// <summary>A numeric, length or percentage literal.</summary>
    Number,
}

/// <summary>A semantic token over an original source span.</summary>
public sealed record MarkupSemanticToken(
    int Offset,
    int Length,
    MarkupSemanticTokenKind Kind);

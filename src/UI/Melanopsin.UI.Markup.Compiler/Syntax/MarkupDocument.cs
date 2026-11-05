// SPDX-FileCopyrightText: 2026 Melanopsin Project Contributors
//
// SPDX-License-Identifier: MIT

using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupDocument {
    public MarkupDocument(
        string path,
        string source,
        IReadOnlyList<MarkupNode> nodes,
        string? namespaceName = null,
        string? componentName = null,
        string? resourcePackage = null,
        IReadOnlyCollection<string>? namespaceUris = null,
        IReadOnlyList<MarkupCode>? codeBlocks = null,
        IReadOnlyList<MarkupDiagnostic>? diagnostics = null) {
        Path = path;
        Source = source;
        Nodes = nodes;
        Namespace = namespaceName;
        ComponentName = componentName;
        ResourcePackage = resourcePackage;
        NamespaceUris = namespaceUris ?? Array.Empty<string>();
        CodeBlocks = codeBlocks ?? Array.Empty<MarkupCode>();
        Diagnostics = diagnostics ?? Array.Empty<MarkupDiagnostic>();
    }

    public string Path { get; }

    public string Source { get; }

    public string? Namespace { get; }

    public string? ComponentName { get; }

    public string? ResourcePackage { get; }

    public IReadOnlyCollection<string> NamespaceUris { get; }

    /// <summary>C# declared inline by <c>@code</c> blocks, in document order.</summary>
    public IReadOnlyList<MarkupCode> CodeBlocks { get; }

    public IReadOnlyList<MarkupNode> Nodes { get; }

    public IReadOnlyList<MarkupDiagnostic> Diagnostics { get; }
}

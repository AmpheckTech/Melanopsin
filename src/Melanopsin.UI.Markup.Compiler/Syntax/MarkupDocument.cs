using Melanopsin.UI.Markup.Compiler.Diagnostics;

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupDocument {
    public MarkupDocument(
        string path,
        string source,
        IReadOnlyList<MarkupNode> nodes,
        string? namespaceName = null,
        string? className = null,
        string? resourcePackage = null,
        IReadOnlyList<MarkupDiagnostic>? diagnostics = null) {
        Path = path;
        Source = source;
        Nodes = nodes;
        Namespace = namespaceName;
        ClassName = className;
        ResourcePackage = resourcePackage;
        Diagnostics = diagnostics ?? Array.Empty<MarkupDiagnostic>();
    }

    public string Path { get; }

    public string Source { get; }

    public string? Namespace { get; }

    public string? ClassName { get; }

    public string? ResourcePackage { get; }

    public IReadOnlyList<MarkupNode> Nodes { get; }

    public IReadOnlyList<MarkupDiagnostic> Diagnostics { get; }
}

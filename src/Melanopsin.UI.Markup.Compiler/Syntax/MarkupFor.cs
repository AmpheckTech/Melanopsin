namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupFor(
    int Offset,
    string Variable,
    string Iterable,
    string? Track,
    IReadOnlyList<MarkupNode> Body,
    IReadOnlyList<MarkupNode> EmptyBody) : MarkupNode(Offset);

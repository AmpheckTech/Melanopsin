namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupElement(
    int Offset,
    string Name,
    IReadOnlyList<MarkupAttribute> Attributes,
    IReadOnlyList<MarkupNode> Children) : MarkupNode(Offset);

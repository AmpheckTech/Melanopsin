namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupSwitchCase(
    int Offset,
    string Pattern,
    IReadOnlyList<MarkupNode> Body) : MarkupNode(Offset);

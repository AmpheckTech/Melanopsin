namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupSwitchCase(
    int Offset,
    int PatternOffset,
    string Pattern,
    IReadOnlyList<MarkupNode> Body) : MarkupNode(Offset);

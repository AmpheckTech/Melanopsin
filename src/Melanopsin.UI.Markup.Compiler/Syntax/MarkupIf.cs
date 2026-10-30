namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupIf(
    int Offset,
    string Condition,
    IReadOnlyList<MarkupNode> WhenTrue,
    IReadOnlyList<MarkupNode> WhenFalse) : MarkupNode(Offset);

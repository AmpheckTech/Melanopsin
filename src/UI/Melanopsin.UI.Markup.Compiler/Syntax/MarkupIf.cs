namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupIf(
    int Offset,
    int ConditionOffset,
    string Condition,
    IReadOnlyList<MarkupNode> WhenTrue,
    IReadOnlyList<MarkupNode> WhenFalse) : MarkupNode(Offset);

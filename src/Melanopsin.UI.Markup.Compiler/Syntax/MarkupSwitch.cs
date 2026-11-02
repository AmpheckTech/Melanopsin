namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupSwitch(
    int Offset,
    int ExpressionOffset,
    string Expression,
    IReadOnlyList<MarkupSwitchCase> Cases,
    IReadOnlyList<MarkupNode> DefaultBody) : MarkupNode(Offset);

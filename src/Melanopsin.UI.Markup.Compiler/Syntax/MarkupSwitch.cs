namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupSwitch(
    int Offset,
    string Expression,
    IReadOnlyList<MarkupSwitchCase> Cases,
    IReadOnlyList<MarkupNode> DefaultBody) : MarkupNode(Offset);

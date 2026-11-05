namespace Melanopsin.UI.Markup.Compiler.Syntax;

/// <summary>
/// An interpolation in element content. <paramref name="Offset"/> is the position of the opening
/// braces, <paramref name="ExpressionOffset"/> the position of the expression itself.
/// </summary>
internal sealed record MarkupExpression(int Offset, int ExpressionOffset, string Expression)
    : MarkupNode(Offset);

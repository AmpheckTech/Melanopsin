namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupExpression(int Offset, string Expression) : MarkupNode(Offset);

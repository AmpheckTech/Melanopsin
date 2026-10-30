namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupText(int Offset, string Text) : MarkupNode(Offset);

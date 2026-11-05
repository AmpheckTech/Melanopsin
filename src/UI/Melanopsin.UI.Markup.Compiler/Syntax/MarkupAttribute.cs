namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupAttribute(
    int Offset,
    string Name,
    IReadOnlyList<MarkupValuePart> Parts);

namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupValuePart(string Value, MarkupValuePartKind Kind) {
    public bool IsExpression => Kind == MarkupValuePartKind.Expression;

    public bool IsResource => Kind == MarkupValuePartKind.Resource;
}

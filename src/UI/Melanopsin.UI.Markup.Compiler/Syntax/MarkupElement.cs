namespace Melanopsin.UI.Markup.Compiler.Syntax;

internal sealed record MarkupElement(
    int Offset,
    string NamespaceUri,
    string Prefix,
    string Name,
    IReadOnlyList<MarkupAttribute> Attributes,
    IReadOnlyList<MarkupNode> Children) : MarkupNode(Offset) {
    public string QualifiedName => Prefix.Length == 0
        ? Name
        : Prefix + ":" + Name;
}

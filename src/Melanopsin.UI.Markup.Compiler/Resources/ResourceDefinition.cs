namespace Melanopsin.UI.Markup.Compiler.Resources;

internal sealed record ResourceDefinition(
    string Category,
    string Name,
    string Id,
    string Kind,
    string ValueTypeName,
    IReadOnlyList<string> Variants);

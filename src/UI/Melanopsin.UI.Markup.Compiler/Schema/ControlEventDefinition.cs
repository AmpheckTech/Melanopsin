namespace Melanopsin.UI.Markup.Compiler.Schema;

internal sealed record ControlEventDefinition(
    string Name,
    string DelegateTypeName,
    int ParameterCount,
    string? ArgumentTypeName);

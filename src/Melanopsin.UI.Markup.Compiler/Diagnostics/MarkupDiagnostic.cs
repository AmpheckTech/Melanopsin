namespace Melanopsin.UI.Markup.Compiler.Diagnostics;

internal sealed record MarkupDiagnostic(
    string Code,
    string Message,
    int Offset,
    int Length);
